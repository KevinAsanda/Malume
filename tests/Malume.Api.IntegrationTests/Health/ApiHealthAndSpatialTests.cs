using System.Net;
using System.Net.Http.Json;
using Malume.Api.IntegrationTests.Infrastructure;
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
}
