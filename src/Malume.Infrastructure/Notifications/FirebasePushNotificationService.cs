using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Malume.Application.Abstractions.Notifications;
using Malume.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Malume.Infrastructure.Notifications;

internal sealed partial class FirebasePushNotificationService(
    IMalumeDbContext dbContext,
    IOptions<FirebaseOptions> options,
    ILogger<FirebasePushNotificationService> logger) : IPushNotificationService
{
    private const int FcmBatchLimit = 500;
    private static readonly object AppInitLock = new();

    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly FirebaseOptions _options = options.Value;
    private readonly ILogger<FirebasePushNotificationService> _logger = logger;

    public async Task<int> SendToUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        PushNotificationMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        ArgumentNullException.ThrowIfNull(message);

        if (!_options.Enabled || userIds.Count == 0)
        {
            return 0;
        }

        FirebaseMessaging? messaging = TryGetFirebaseMessaging();
        if (messaging is null)
        {
            return 0;
        }

        List<Guid> distinctUsers = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (distinctUsers.Count == 0)
        {
            return 0;
        }

        List<string> tokens = await _dbContext.UserDeviceTokens
            .AsNoTracking()
            .Where(t => distinctUsers.Contains(t.UserId))
            .Select(t => t.Token)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (tokens.Count == 0)
        {
            return 0;
        }

        int totalDelivered = 0;
        Dictionary<string, string>? payloadData = message.Data is null
            ? null
            : new Dictionary<string, string>(message.Data, StringComparer.Ordinal);

        for (int offset = 0; offset < tokens.Count; offset += FcmBatchLimit)
        {
            List<string> batch = tokens.Skip(offset).Take(FcmBatchLimit).ToList();
            var multicast = new MulticastMessage
            {
                Fids = batch,
                Notification = new Notification
                {
                    Title = message.Title,
                    Body = message.Body
                },
                Data = payloadData
            };

            try
            {
                BatchResponse response = await messaging.SendEachForMulticastAsync(multicast, cancellationToken);
                totalDelivered += response.SuccessCount;
                if (response.FailureCount > 0)
                {
                    LogPartialFailure(_logger, response.FailureCount, batch.Count);
                }
            }
            catch (FirebaseMessagingException ex)
            {
                LogMessagingException(_logger, ex);
            }
        }

        return totalDelivered;
    }

    private FirebaseMessaging? TryGetFirebaseMessaging()
    {
        lock (AppInitLock)
        {
            if (FirebaseApp.DefaultInstance is not null)
            {
                return FirebaseMessaging.DefaultInstance;
            }

            GoogleCredential? credential = ResolveCredential();
            if (credential is null)
            {
                return null;
            }

            FirebaseApp.Create(new AppOptions
            {
                Credential = credential
            });

            return FirebaseMessaging.DefaultInstance;
        }
    }

    private GoogleCredential? ResolveCredential()
    {
        if (!string.IsNullOrWhiteSpace(_options.ServiceAccountJson))
        {
#pragma warning disable CS0618
            return GoogleCredential.FromJson(_options.ServiceAccountJson);
#pragma warning restore CS0618
        }

        if (!string.IsNullOrWhiteSpace(_options.ServiceAccountFilePath)
            && File.Exists(_options.ServiceAccountFilePath))
        {
#pragma warning disable CS0618
            return GoogleCredential.FromFile(_options.ServiceAccountFilePath);
#pragma warning restore CS0618
        }

        string? envCredPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
        if (!string.IsNullOrWhiteSpace(envCredPath) && File.Exists(envCredPath))
        {
            return GoogleCredential.GetApplicationDefault();
        }

        return null;
    }

    [LoggerMessage(
        EventId = 6101,
        Level = LogLevel.Warning,
        Message = "FCM multicast finished with {FailureCount}/{TotalCount} failed token deliveries.")]
    private static partial void LogPartialFailure(ILogger logger, int failureCount, int totalCount);

    [LoggerMessage(
        EventId = 6102,
        Level = LogLevel.Warning,
        Message = "FCM dispatch failed cleanly without blocking core workflow.")]
    private static partial void LogMessagingException(ILogger logger, Exception exception);
}
