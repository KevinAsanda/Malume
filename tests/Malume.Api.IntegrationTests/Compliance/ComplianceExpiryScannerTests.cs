using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Malume.Api.IntegrationTests.Infrastructure;
using Malume.Domain.Common;
using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Domain.ValueObjects;
using Malume.Infrastructure.Persistence;
using Malume.Shared.Contracts.Auth;
using Malume.Shared.Contracts.Compliance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Malume.Api.IntegrationTests.Compliance;

public sealed class ComplianceExpiryScannerTests(MalumeApiFactory factory) : IClassFixture<MalumeApiFactory>
{
    private readonly MalumeApiFactory _factory = factory;

    [Fact]
    public async Task RunComplianceScan_ShouldFlagExpiringSoonSuspendExpiredAndPauseEscrow()
    {
        DateOnly today = SastClock.TodaySast(DateTimeOffset.UtcNow);
        Guid driverAId;
        Guid driverBId;
        Guid subscriptionId;

        using (IServiceScope seedScope = _factory.Services.CreateScope())
        {
            MalumeDbContext db = seedScope.ServiceProvider.GetRequiredService<MalumeDbContext>();

            var driverAUser = new User($"scanA-{Guid.NewGuid():N}@malume.test", "Driver A", "+27821000001", "hash", UserRole.Driver);
            var driverBUser = new User($"scanB-{Guid.NewGuid():N}@malume.test", "Driver B", "+27821000002", "hash", UserRole.Driver);
            var parentUser = new User($"parent-{Guid.NewGuid():N}@malume.test", "Parent Scan", "+27821000003", "hash", UserRole.Parent);
            var school = new School("Scan High School", new GeoCoordinate(-26.1076, 28.0570), "10 School St");

            var driverA = new DriverProfile(driverAUser.Id, $"PRDP-A-{Guid.NewGuid():N}"[..16]);
            driverA.ApproveCompliance(today.AddDays(10), today.AddYears(1));

            var driverB = new DriverProfile(driverBUser.Id, $"PRDP-B-{Guid.NewGuid():N}"[..16]);
            driverB.ApproveCompliance(today.AddDays(-1), today.AddYears(1));

            var vehicleB = new Vehicle(
                $"SCAN{Guid.NewGuid().ToString("N")[..5].ToUpperInvariant()}",
                "Toyota Quantum",
                15,
                today.AddYears(1),
                today.AddYears(1),
                assignedDriverId: driverB.Id);

            var routeB = new ScheduledRoute(
                vehicleB.Id,
                driverB.Id,
                school.Id,
                "Scan Route",
                RouteDirection.MorningToSchool,
                new GeoCorridor([new GeoCoordinate(-26.2041, 28.0473), new GeoCoordinate(-26.1076, 28.0570)]),
                ZarMoney.Of(1200m));

            var learner = new Learner(
                parentUser.Id,
                school.Id,
                "Lerato Mokoena",
                "Grade 7",
                new GeoCoordinate(-26.2041, 28.0473),
                "12 Main Rd",
                "+27821000099");

            Subscription subscription = Subscription.CreateWithStagedEscrow(
                parentUser.Id,
                learner.Id,
                routeB.Id,
                $"{today:yyyy-MM}",
                ZarMoney.Of(1200m),
                new DateOnly(today.Year, today.Month, 1));

            db.Users.AddRange(driverAUser, driverBUser, parentUser);
            db.Schools.Add(school);
            db.DriverProfiles.AddRange(driverA, driverB);
            db.Vehicles.Add(vehicleB);
            db.ScheduledRoutes.Add(routeB);
            db.Learners.Add(learner);
            db.Subscriptions.Add(subscription);
            await db.SaveChangesAsync();

            driverAId = driverA.Id;
            driverBId = driverB.Id;
            subscriptionId = subscription.Id;
        }

        using HttpClient adminClient = _factory.CreateClient();
        HttpResponseMessage loginResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("sysadmin@malume.test", "MalumeAdmin!2026"));
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        AuthResponse? adminAuth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        adminAuth.ShouldNotBeNull();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);

        HttpResponseMessage scanResponse = await adminClient.PostAsync("/api/v1/admin/jobs/compliance-scan", null);
        scanResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        ComplianceScanResultDto? result = await scanResponse.Content.ReadFromJsonAsync<ComplianceScanResultDto>();
        result.ShouldNotBeNull();
        result.MarkedExpiringSoon.ShouldBeGreaterThanOrEqualTo(1);
        result.Suspended.ShouldBeGreaterThanOrEqualTo(1);
        result.SubscriptionsPaused.ShouldBeGreaterThanOrEqualTo(1);

        using IServiceScope verifyScope = _factory.Services.CreateScope();
        MalumeDbContext verifyDb = verifyScope.ServiceProvider.GetRequiredService<MalumeDbContext>();

        DriverProfile? updatedA = await verifyDb.DriverProfiles.AsNoTracking().FirstOrDefaultAsync(d => d.Id == driverAId);
        DriverProfile? updatedB = await verifyDb.DriverProfiles.AsNoTracking().FirstOrDefaultAsync(d => d.Id == driverBId);
        Subscription? updatedSub = await verifyDb.Subscriptions
            .Include(s => s.Milestones)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == subscriptionId);

        updatedA.ShouldNotBeNull();
        updatedA.ComplianceStatus.ShouldBe(DriverComplianceStatus.ActiveExpiringSoon);

        updatedB.ShouldNotBeNull();
        updatedB.ComplianceStatus.ShouldBe(DriverComplianceStatus.SuspendedExpiredDoc);

        updatedSub.ShouldNotBeNull();
        updatedSub.EscrowStatus.ShouldBe(EscrowStatus.PausedComplianceOrServiceFailure);
        updatedSub.Milestones.Count.ShouldBe(3);
        updatedSub.Milestones.ShouldAllBe(m => m.Status == MilestoneStatus.Frozen);
    }
}
