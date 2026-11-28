using System.Net;
using System.Net.Http.Json;
using Malume.Api.IntegrationTests.Infrastructure;
using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Domain.ValueObjects;
using Malume.Infrastructure.Persistence;
using Malume.Shared.Contracts.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Shouldly;

namespace Malume.Api.IntegrationTests.Health;

public sealed class ApiHealthAndSpatialTests(MalumeApiFactory factory) : IClassFixture<MalumeApiFactory>
{
    private readonly MalumeApiFactory _factory = factory;

    [Fact]
    public async Task GetHealth_ShouldReturnHealthy()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        HealthStatusResponse? payload = await response.Content.ReadFromJsonAsync<HealthStatusResponse>();
        payload.ShouldNotBeNull();
        payload.Service.ShouldBe("Malume.Api");
        payload.Status.ShouldBe("Healthy");
    }

    [Fact]
    public async Task GetDatabaseHealth_ShouldConnectToTestcontainersSqlServer()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/health/database");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        HealthStatusResponse? payload = await response.Content.ReadFromJsonAsync<HealthStatusResponse>();
        payload.ShouldNotBeNull();
        payload.Status.ShouldBe("Healthy");
    }

    [Fact]
    public async Task SpatialQuery_ShouldComputeCorridorDistanceInSqlServer()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        MalumeDbContext dbContext = scope.ServiceProvider.GetRequiredService<MalumeDbContext>();

        GeometryFactory geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        Point homePoint = geometryFactory.CreatePoint(new Coordinate(28.0473, -26.2041));
        LineString corridor = geometryFactory.CreateLineString(
        [
            new Coordinate(28.0473, -26.2041),
            new Coordinate(28.0570, -26.1076)
        ]);

        double distanceMeters = await dbContext.Database
            .SqlQuery<double>($"SELECT {corridor}.STDistance({homePoint}) AS [Value]")
            .SingleAsync();

        distanceMeters.ShouldBe(0.0, tolerance: 0.01);
    }

    [Fact]
    public async Task ScheduledRoute_ShouldPersistGeographyAndEvaluate1500mCorridorInSqlServer()
    {
        using IServiceScope writeScope = _factory.Services.CreateScope();
        MalumeDbContext writeDb = writeScope.ServiceProvider.GetRequiredService<MalumeDbContext>();

        var driverUser = new User(
            $"driver-{Guid.NewGuid():N}@malume.co.za",
            "Sipho Mokoena",
            "+27821112222",
            "PBKDF2-SHA256.100000.salt.hash",
            UserRole.Driver);
        var school = new School(
            "Sandton Primary School",
            new GeoCoordinate(-26.1076, 28.0570),
            "100 Rivonia Rd, Sandton");
        var driver = new DriverProfile(driverUser.Id, $"PRDP-{Guid.NewGuid():N}"[..16]);
        var vehicle = new Vehicle(
            $"GP{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            "Toyota Quantum 2.7",
            15,
            new DateOnly(2027, 6, 30),
            new DateOnly(2027, 6, 30),
            assignedDriverId: driver.Id);
        var route = new ScheduledRoute(
            vehicle.Id,
            driver.Id,
            school.Id,
            "JHB CBD -> Sandton Morning Corridor",
            RouteDirection.MorningToSchool,
            new GeoCorridor([new GeoCoordinate(-26.2041, 28.0473), new GeoCoordinate(-26.1076, 28.0570)]),
            ZarMoney.Of(1200.00m));

        writeDb.Users.Add(driverUser);
        writeDb.Schools.Add(school);
        writeDb.DriverProfiles.Add(driver);
        writeDb.Vehicles.Add(vehicle);
        writeDb.ScheduledRoutes.Add(route);
        await writeDb.SaveChangesAsync();

        using IServiceScope readScope = _factory.Services.CreateScope();
        MalumeDbContext readDb = readScope.ServiceProvider.GetRequiredService<MalumeDbContext>();

        ScheduledRoute? persisted = await readDb.ScheduledRoutes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == route.Id);

        persisted.ShouldNotBeNull();
        persisted.CorridorPath.Points.Count.ShouldBe(2);
        persisted.MonthlyPrice.Amount.ShouldBe(1200.00m);

        GeometryFactory geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        Point onCorridorPoint = geometryFactory.CreatePoint(new Coordinate(28.0473, -26.2041));
        Point farPoint = geometryFactory.CreatePoint(new Coordinate(28.2293, -25.7479));

        double nearDistance = await readDb.Database
            .SqlQuery<double>($"SELECT CorridorPath.STDistance({onCorridorPoint}) AS [Value] FROM ScheduledRoutes WHERE RouteId = {route.Id}")
            .SingleAsync();
        double farDistance = await readDb.Database
            .SqlQuery<double>($"SELECT CorridorPath.STDistance({farPoint}) AS [Value] FROM ScheduledRoutes WHERE RouteId = {route.Id}")
            .SingleAsync();

        nearDistance.ShouldBeLessThanOrEqualTo(1500.0);
        farDistance.ShouldBeGreaterThan(1500.0);
    }
}
