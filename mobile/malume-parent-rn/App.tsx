import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  StatusBar,
  StyleSheet,
  Text,
  TouchableOpacity,
  useColorScheme,
  View,
} from 'react-native';
import { SafeAreaProvider, SafeAreaView } from 'react-native-safe-area-context';
import { CorridorSearchScreen } from './src/screens/CorridorSearchScreen';
import { HomeTripsScreen } from './src/screens/HomeTripsScreen';
import { LearnersScreen } from './src/screens/LearnersScreen';
import { LoginScreen } from './src/screens/LoginScreen';
import { SubscriptionsScreen } from './src/screens/SubscriptionsScreen';
import { MalumeParentApiClient } from './src/services/apiClient';
import { ParentRealtimeClient } from './src/services/realtimeClient';
import {
  AuthResponse,
  CorridorAdHocOptionDto,
  CreateLearnerRequest,
  DailyPinDto,
  DriverTelemetryPingDto,
  EtaCascadeUpdateDto,
  InitializeSubscriptionPaymentResponse,
  LearnerDto,
  LearnerQrBadgeDto,
  OffPeakDriverOptionDto,
  ParentActiveTripDto,
  ScheduledRouteDto,
  SchoolDto,
  SosAlertDto,
  SubscriptionEscrowDto,
} from './src/types/api';
import { formatSastDate } from './src/utils/formatters';

type TabName = 'trips' | 'learners' | 'corridor' | 'subscriptions';

function App(): React.JSX.Element {
  const isDarkMode = useColorScheme() === 'dark';

  return (
    <SafeAreaProvider>
      <StatusBar barStyle={isDarkMode ? 'light-content' : 'dark-content'} />
      <SafeAreaView style={styles.root}>
        <ParentAppShell />
      </SafeAreaView>
    </SafeAreaProvider>
  );
}

function ParentAppShell(): React.JSX.Element {
  const apiClient = useMemo(() => new MalumeParentApiClient(), []);
  const realtimeRef = useRef(new ParentRealtimeClient());

  const [session, setSession] = useState<AuthResponse | null>(null);
  const [activeTab, setActiveTab] = useState<TabName>('trips');
  const [isLoading, setIsLoading] = useState(false);
  const [statusBanner, setStatusBanner] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const [schools, setSchools] = useState<SchoolDto[]>([]);
  const [learners, setLearners] = useState<LearnerDto[]>([]);
  const [activeTrips, setActiveTrips] = useState<ParentActiveTripDto[]>([]);
  const [scheduledRoutes, setScheduledRoutes] = useState<ScheduledRouteDto[]>([]);
  const [escrowSubscriptions, setEscrowSubscriptions] = useState<SubscriptionEscrowDto[]>([]);
  const [pendingCheckout, setPendingCheckout] =
    useState<InitializeSubscriptionPaymentResponse | null>(null);
  const [corridorOptions, setCorridorOptions] = useState<CorridorAdHocOptionDto[]>([]);
  const [offPeakDrivers, setOffPeakDrivers] = useState<OffPeakDriverOptionDto[]>([]);
  const [activePass, setActivePass] = useState<{
    learnerName: string;
    qr?: LearnerQrBadgeDto;
    pin?: DailyPinDto;
  } | null>(null);

  const [realtimeConnected, setRealtimeConnected] = useState(false);
  const [livePingsByTrip, setLivePingsByTrip] = useState<Record<string, DriverTelemetryPingDto>>(
    {},
  );
  const [cascadeAlerts, setCascadeAlerts] = useState<EtaCascadeUpdateDto[]>([]);
  const [sosAlerts, setSosAlerts] = useState<SosAlertDto[]>([]);

  const loadDashboardData = useCallback(async () => {
    setIsLoading(true);
    setErrorMessage(null);
    try {
      const [schoolList, learnerList, tripsList, routeList, subscriptionList] = await Promise.all([
        apiClient.listSchools(),
        apiClient.listLearners(),
        apiClient.getParentActiveTrips(formatSastDate()),
        apiClient.listScheduledRoutes(),
        apiClient.listEscrowSubscriptions(),
      ]);
      setSchools(schoolList);
      setLearners(learnerList);
      setActiveTrips(tripsList);
      setScheduledRoutes(routeList);
      setEscrowSubscriptions(subscriptionList);
    } catch (err) {
      setErrorMessage(err instanceof Error ? err.message : 'Unable to load parent dashboard.');
    } finally {
      setIsLoading(false);
    }
  }, [apiClient]);

  useEffect(() => {
    const realtime = realtimeRef.current;
    if (!session) {
      realtime.disconnect();
      return;
    }

    const tripIds = Array.from(new Set(activeTrips.map(t => t.tripId)));
    realtime
      .connect(apiClient.getBaseUrl(), session.accessToken, tripIds, {
        onConnected: () => setRealtimeConnected(true),
        onDisconnected: () => setRealtimeConnected(false),
        onLocationUpdated: ping =>
          setLivePingsByTrip(prev => ({
            ...prev,
            [ping.tripId]: ping,
          })),
        onEtaCascade: update => {
          setCascadeAlerts(prev => [update, ...prev.slice(0, 4)]);
          void loadDashboardData();
        },
        onHandoverRecorded: () => {
          void loadDashboardData();
        },
        onTripStatusChanged: () => {
          void loadDashboardData();
        },
        onSosAlert: alert => {
          setSosAlerts(prev => [alert, ...prev.slice(0, 2)]);
        },
      })
      .catch(() => {
        setRealtimeConnected(false);
      });

    return () => {
      realtime.disconnect();
    };
  }, [session, activeTrips, apiClient, loadDashboardData]);

  const handleLogin = async (baseUrl: string, email: string, password: string) => {
    setIsLoading(true);
    setErrorMessage(null);
    try {
      apiClient.setBaseUrl(baseUrl);
      const auth = await apiClient.login(email, password);
      setSession(auth);
      await loadDashboardData();
    } catch (err) {
      setErrorMessage(err instanceof Error ? err.message : 'Sign-in failed.');
    } finally {
      setIsLoading(false);
    }
  };

  const handleRegister = async (
    baseUrl: string,
    email: string,
    fullName: string,
    phoneNumber: string,
    password: string,
  ) => {
    setIsLoading(true);
    setErrorMessage(null);
    try {
      apiClient.setBaseUrl(baseUrl);
      const auth = await apiClient.registerParent(email, fullName, phoneNumber, password);
      setSession(auth);
      await loadDashboardData();
    } catch (err) {
      setErrorMessage(err instanceof Error ? err.message : 'Registration failed.');
    } finally {
      setIsLoading(false);
    }
  };

  const handleLogout = () => {
    realtimeRef.current.disconnect();
    apiClient.setAccessToken(null);
    setSession(null);
    setActiveTrips([]);
    setLearners([]);
    setScheduledRoutes([]);
    setEscrowSubscriptions([]);
    setPendingCheckout(null);
    setCorridorOptions([]);
    setCascadeAlerts([]);
    setSosAlerts([]);
    setStatusBanner(null);
    setErrorMessage(null);
  };

  const handleCreateLearner = async (payload: CreateLearnerRequest) => {
    setIsLoading(true);
    setErrorMessage(null);
    try {
      await apiClient.createLearner(payload);
      setStatusBanner(`Registered learner ${payload.fullName}.`);
      await loadDashboardData();
    } catch (err) {
      setErrorMessage(err instanceof Error ? err.message : 'Unable to register learner.');
    } finally {
      setIsLoading(false);
    }
  };

  const handleLoadSecurityPass = async (
    learnerId: string,
    direction: 'MorningToSchool' | 'AfternoonFromSchool',
  ) => {
    setErrorMessage(null);
    try {
      const learner = learners.find(l => l.learnerId === learnerId);
      const [qr, pin] = await Promise.all([
        apiClient.getLearnerQrBadge(learnerId),
        apiClient.getLearnerDailyPin(learnerId, formatSastDate(), direction),
      ]);
      setActivePass({
        learnerName: learner?.fullName ?? qr.learnerFullName,
        qr,
        pin,
      });
    } catch (err) {
      setErrorMessage(err instanceof Error ? err.message : 'Unable to load QR/PIN pass.');
    }
  };

  const handleMarkAbsent = async (tripId: string, learnerId: string) => {
    setErrorMessage(null);
    setStatusBanner(null);
    try {
      const res = await apiClient.markLearnerAbsent(tripId, learnerId);
      setStatusBanner(
        `Marked absent. ${res.availableCorridorSeats} corridor seat(s) now available; ${res.downstreamEtaUpdates.length} parent ETA notification(s) dispatched.`,
      );
      await loadDashboardData();
    } catch (err) {
      setErrorMessage(err instanceof Error ? err.message : 'Unable to mark absent.');
    }
  };

  const handleSearchCorridor = async (schoolId: string, lat: number, lng: number) => {
    setErrorMessage(null);
    setStatusBanner(null);
    try {
      const matches = await apiClient.searchCorridorOptions({
        schoolId,
        pickupLatitude: lat,
        pickupLongitude: lng,
        serviceDate: formatSastDate(),
      });
      setCorridorOptions(matches);
      setStatusBanner(`Found ${matches.length} active route(s) within 1.5 km corridor.`);
    } catch (err) {
      setErrorMessage(err instanceof Error ? err.message : 'Corridor search failed.');
    }
  };

  const handleBookCorridorSeat = async (
    tripId: string,
    learnerId: string,
    lat: number,
    lng: number,
  ) => {
    setErrorMessage(null);
    setStatusBanner(null);
    try {
      const booked = await apiClient.bookCorridorSeat(tripId, learnerId, lat, lng);
      setStatusBanner(
        `Corridor seat confirmed! Stop #${booked.sequenceOrder} (${booked.remainingCorridorSeats} seats remaining).`,
      );
      setActiveTab('trips');
      await loadDashboardData();
    } catch (err) {
      setErrorMessage(err instanceof Error ? err.message : 'Booking failed.');
    }
  };

  const handleSearchOffPeak = async (lat: number, lng: number) => {
    setErrorMessage(null);
    try {
      const drivers = await apiClient.searchOffPeakDrivers(lat, lng, 5000);
      setOffPeakDrivers(drivers);
      setStatusBanner(`Found ${drivers.length} off-peak compliant driver(s) within 5 km.`);
    } catch (err) {
      setErrorMessage(err instanceof Error ? err.message : 'Off-peak search failed.');
    }
  };

  const handleTriggerSos = async (tripId: string, latitude: number, longitude: number) => {
    setErrorMessage(null);
    setStatusBanner(null);
    try {
      const alert = await apiClient.triggerTripSos(tripId, latitude, longitude);
      setSosAlerts(prev => [alert, ...prev.slice(0, 2)]);
      setStatusBanner(`SOS Alert dispatched for ${alert.vehicleRegistration}. Help notified.`);
    } catch (err) {
      setErrorMessage(err instanceof Error ? err.message : 'Unable to dispatch SOS alert.');
    }
  };

  const handleInitializePayment = async (routeId: string, learnerId: string) => {
    setErrorMessage(null);
    setStatusBanner(null);
    try {
      const res = await apiClient.initializeSubscriptionPayment({ routeId, learnerId });
      setPendingCheckout(res);
      setStatusBanner(
        `Paystack checkout initialized (${res.paystackReference}) for R ${res.totalAmountZar.toFixed(2)}.`,
      );
      await loadDashboardData();
    } catch (err) {
      setErrorMessage(err instanceof Error ? err.message : 'Unable to initialize Paystack checkout.');
    }
  };

  const handleVerifyPayment = async (reference: string) => {
    setErrorMessage(null);
    setStatusBanner(null);
    try {
      const verified = await apiClient.verifySubscriptionPayment(reference);
      setStatusBanner(
        verified.paymentVerified
          ? `Payment verified! Escrow status: ${verified.escrowStatus}.`
          : `Payment reference ${verified.paystackReference} is still ${verified.escrowStatus}.`,
      );
      if (verified.paymentVerified) {
        setPendingCheckout(null);
      }
      await loadDashboardData();
    } catch (err) {
      setErrorMessage(err instanceof Error ? err.message : 'Payment verification failed.');
    }
  };

  if (!session) {
    return (
      <LoginScreen
        initialBaseUrl={apiClient.getBaseUrl()}
        isLoading={isLoading}
        errorMessage={errorMessage}
        onLogin={handleLogin}
        onRegister={handleRegister}
      />
    );
  }

  return (
    <View style={styles.root}>
      <View style={styles.topBar}>
        <Text style={styles.topTitle}>Malume Parent • {session.fullName}</Text>
        <TouchableOpacity onPress={handleLogout}>
          <Text style={styles.logoutText}>Logout</Text>
        </TouchableOpacity>
      </View>

      {errorMessage ? (
        <View style={styles.errorBanner}>
          <Text style={styles.errorBannerText}>{errorMessage}</Text>
        </View>
      ) : null}

      {statusBanner ? (
        <View style={styles.infoBanner}>
          <Text style={styles.infoBannerText}>{statusBanner}</Text>
        </View>
      ) : null}

      <View style={styles.body}>
        {activeTab === 'trips' && (
          <HomeTripsScreen
            trips={activeTrips}
            livePingsByTrip={livePingsByTrip}
            cascadeAlerts={cascadeAlerts}
            sosAlerts={sosAlerts}
            realtimeConnected={realtimeConnected}
            isLoading={isLoading}
            onRefresh={() => {
              void loadDashboardData();
            }}
            onMarkAbsent={(tripId, learnerId) => {
              void handleMarkAbsent(tripId, learnerId);
            }}
            onTriggerSos={(tripId, lat, lng) => {
              void handleTriggerSos(tripId, lat, lng);
            }}
          />
        )}

        {activeTab === 'learners' && (
          <LearnersScreen
            learners={learners}
            schools={schools}
            activePass={activePass}
            onCreateLearner={payload => {
              void handleCreateLearner(payload);
            }}
            onLoadSecurityPass={(learnerId, direction) => {
              void handleLoadSecurityPass(learnerId, direction);
            }}
            onClosePass={() => setActivePass(null)}
          />
        )}

        {activeTab === 'corridor' && (
          <CorridorSearchScreen
            learners={learners}
            schools={schools}
            corridorOptions={corridorOptions}
            offPeakDrivers={offPeakDrivers}
            onSearchCorridor={(schoolId, lat, lng) => {
              void handleSearchCorridor(schoolId, lat, lng);
            }}
            onBookCorridorSeat={(tripId, learnerId, lat, lng) => {
              void handleBookCorridorSeat(tripId, learnerId, lat, lng);
            }}
            onSearchOffPeak={(lat, lng) => {
              void handleSearchOffPeak(lat, lng);
            }}
          />
        )}

        {activeTab === 'subscriptions' && (
          <SubscriptionsScreen
            learners={learners}
            routes={scheduledRoutes}
            subscriptions={escrowSubscriptions}
            pendingCheckout={pendingCheckout}
            onInitializePayment={(routeId, learnerId) => {
              void handleInitializePayment(routeId, learnerId);
            }}
            onVerifyPayment={reference => {
              void handleVerifyPayment(reference);
            }}
          />
        )}
      </View>

      <View style={styles.tabBar}>
        {(
          [
            ['trips', 'My Trips'],
            ['learners', 'Learners & PIN'],
            ['corridor', 'Corridor'],
            ['subscriptions', 'Escrow & Pay'],
          ] as const
        ).map(([tabKey, label]) => (
          <TouchableOpacity
            key={tabKey}
            style={[styles.tabButton, activeTab === tabKey && styles.tabActive]}
            onPress={() => setActiveTab(tabKey)}>
            <Text style={[styles.tabLabel, activeTab === tabKey && styles.tabLabelActive]}>
              {label}
            </Text>
          </TouchableOpacity>
        ))}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  root: { flex: 1, backgroundColor: '#F5F7FA' },
  topBar: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: 16,
    paddingVertical: 12,
    backgroundColor: '#0F172A',
  },
  topTitle: { color: '#FFFFFF', fontWeight: '700', fontSize: 15 },
  logoutText: { color: '#93C5FD', fontWeight: '600', fontSize: 13 },
  errorBanner: { backgroundColor: '#FEE2E2', paddingHorizontal: 14, paddingVertical: 8 },
  errorBannerText: { color: '#991B1B', fontSize: 12 },
  infoBanner: { backgroundColor: '#DCFCE7', paddingHorizontal: 14, paddingVertical: 8 },
  infoBannerText: { color: '#166534', fontSize: 12 },
  body: { flex: 1 },
  tabBar: {
    flexDirection: 'row',
    backgroundColor: '#FFFFFF',
    borderTopWidth: 1,
    borderTopColor: '#CBD5E1',
  },
  tabButton: { flex: 1, paddingVertical: 14, alignItems: 'center' },
  tabActive: { borderTopWidth: 3, borderTopColor: '#1D4ED8' },
  tabLabel: { fontSize: 13, fontWeight: '600', color: '#64748B' },
  tabLabelActive: { color: '#1D4ED8', fontWeight: '700' },
});

export default App;
