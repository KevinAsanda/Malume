import React from 'react';
import {
  ActivityIndicator,
  ScrollView,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import {
  DriverTelemetryPingDto,
  EtaCascadeUpdateDto,
  ParentActiveTripDto,
  SosAlertDto,
} from '../types/api';
import { formatEtaTime } from '../utils/formatters';

interface HomeTripsScreenProps {
  trips: ParentActiveTripDto[];
  livePingsByTrip: Record<string, DriverTelemetryPingDto>;
  cascadeAlerts: EtaCascadeUpdateDto[];
  sosAlerts: SosAlertDto[];
  realtimeConnected: boolean;
  isLoading: boolean;
  onRefresh: () => void;
  onMarkAbsent: (tripId: string, learnerId: string) => void;
}

export function HomeTripsScreen({
  trips,
  livePingsByTrip,
  cascadeAlerts,
  sosAlerts,
  realtimeConnected,
  isLoading,
  onRefresh,
  onMarkAbsent,
}: HomeTripsScreenProps): React.JSX.Element {
  return (
    <ScrollView style={styles.container} contentContainerStyle={styles.content}>
      <View style={styles.headerRow}>
        <View>
          <Text style={styles.heading}>Today's Scholar Trips</Text>
          <Text style={styles.badge}>
            SignalR Live: {realtimeConnected ? 'Connected' : 'Reconnecting / Idle'}
          </Text>
        </View>
        <TouchableOpacity style={styles.refreshBtn} onPress={onRefresh}>
          <Text style={styles.refreshBtnText}>Refresh</Text>
        </TouchableOpacity>
      </View>

      {isLoading ? <ActivityIndicator style={styles.loader} /> : null}

      {sosAlerts.map((alert, idx) => (
        <View key={`sos-${alert.tripId}-${idx}`} style={styles.sosBanner}>
          <Text style={styles.sosTitle}>SOS ALERT — {alert.vehicleRegistration}</Text>
          <Text style={styles.sosBody}>
            Driver {alert.driverName} triggered SOS at {alert.latitude.toFixed(4)},{' '}
            {alert.longitude.toFixed(4)}
          </Text>
        </View>
      ))}

      {cascadeAlerts.map((alert, idx) => (
        <View key={`eta-${alert.learnerId}-${idx}`} style={styles.etaBanner}>
          <Text style={styles.etaTitle}>Updated ETA (+{alert.minutesEarlier} min earlier)</Text>
          <Text style={styles.etaBody}>
            A stop ahead on {alert.learnerFullName}'s route was skipped. New arrival:{' '}
            {formatEtaTime(alert.newEtaUtc)} (Stop #{alert.newSequenceOrder})
          </Text>
        </View>
      ))}

      {trips.length === 0 ? (
        <View style={styles.emptyCard}>
          <Text style={styles.emptyText}>
            No active trips scheduled for your learners today. Check the Corridor Rides tab to book
            an empty seat within 1.5 km or register/enroll a learner first.
          </Text>
        </View>
      ) : (
        trips.map(item => {
          const ping = livePingsByTrip[item.tripId];
          const lat = ping?.latitude ?? item.driverLatitude;
          const lng = ping?.longitude ?? item.driverLongitude;
          const canMarkAbsent = item.attendanceStatus === 'Scheduled';

          return (
            <View key={`${item.tripId}-${item.learnerId}`} style={styles.card}>
              <View style={styles.cardTop}>
                <Text style={styles.learnerTitle}>{item.learnerFullName}</Text>
                <Text style={styles.statusChip}>{item.attendanceStatus}</Text>
              </View>
              <Text style={styles.routeLine}>
                {item.routeName} ({item.direction})
              </Text>
              <Text style={styles.meta}>
                School: {item.schoolName} | Vehicle: {item.vehicleRegistration}
              </Text>
              <Text style={styles.meta}>
                Driver: {item.driverFullName} | Trip Status: {item.tripStatus}
              </Text>
              <Text style={styles.etaHighlight}>
                Stop #{item.sequenceOrder} • ETA: {formatEtaTime(item.estimatedArrivalUtc)}
              </Text>
              {lat != null && lng != null ? (
                <Text style={styles.gpsLine}>
                  Live GPS: {lat.toFixed(5)}, {lng.toFixed(5)}
                  {ping ? ` (${ping.speedKph.toFixed(0)} km/h)` : ''}
                </Text>
              ) : null}

              {canMarkAbsent ? (
                <TouchableOpacity
                  style={styles.absentBtn}
                  onPress={() => onMarkAbsent(item.tripId, item.learnerId)}>
                  <Text style={styles.absentBtnText}>Mark Absent Today</Text>
                </TouchableOpacity>
              ) : null}
            </View>
          );
        })
      )}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#F5F7FA' },
  content: { padding: 16, gap: 12 },
  headerRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  heading: { fontSize: 20, fontWeight: '700', color: '#0F172A' },
  badge: { fontSize: 12, color: '#047857', marginTop: 2 },
  refreshBtn: {
    borderWidth: 1,
    borderColor: '#1D4ED8',
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 6,
  },
  refreshBtnText: { color: '#1D4ED8', fontWeight: '600' },
  loader: { marginVertical: 8 },
  sosBanner: {
    backgroundColor: '#FEE2E2',
    borderWidth: 1,
    borderColor: '#DC2626',
    borderRadius: 10,
    padding: 12,
  },
  sosTitle: { color: '#991B1B', fontWeight: '700', fontSize: 14 },
  sosBody: { color: '#7F1D1D', fontSize: 13, marginTop: 4 },
  etaBanner: {
    backgroundColor: '#DBEAFE',
    borderWidth: 1,
    borderColor: '#2563EB',
    borderRadius: 10,
    padding: 12,
  },
  etaTitle: { color: '#1E40AF', fontWeight: '700', fontSize: 14 },
  etaBody: { color: '#1E3A8A', fontSize: 13, marginTop: 4 },
  emptyCard: {
    backgroundColor: '#FFFFFF',
    borderRadius: 10,
    padding: 16,
    borderWidth: 1,
    borderColor: '#E2E8F0',
  },
  emptyText: { color: '#475569', fontSize: 14, lineHeight: 20 },
  card: {
    backgroundColor: '#FFFFFF',
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: '#E2E8F0',
    gap: 4,
  },
  cardTop: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  learnerTitle: { fontSize: 17, fontWeight: '700', color: '#0F172A' },
  statusChip: {
    backgroundColor: '#EFF6FF',
    color: '#1D4ED8',
    fontSize: 12,
    fontWeight: '700',
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 6,
  },
  routeLine: { fontSize: 14, fontWeight: '600', color: '#334155' },
  meta: { fontSize: 12, color: '#64748B' },
  etaHighlight: { fontSize: 13, fontWeight: '700', color: '#0369A1', marginTop: 4 },
  gpsLine: { fontSize: 12, color: '#059669' },
  absentBtn: {
    marginTop: 8,
    backgroundColor: '#FEF3C7',
    borderWidth: 1,
    borderColor: '#D97706',
    borderRadius: 8,
    paddingVertical: 10,
    alignItems: 'center',
  },
  absentBtnText: { color: '#92400E', fontWeight: '700', fontSize: 14 },
});
