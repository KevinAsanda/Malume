import React, { useState } from 'react';
import {
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import {
  CorridorAdHocOptionDto,
  LearnerDto,
  OffPeakDriverOptionDto,
  SchoolDto,
} from '../types/api';
import { formatDistanceMeters, isPeakWindowSast } from '../utils/formatters';

interface CorridorSearchScreenProps {
  learners: LearnerDto[];
  schools: SchoolDto[];
  corridorOptions: CorridorAdHocOptionDto[];
  offPeakDrivers: OffPeakDriverOptionDto[];
  onSearchCorridor: (schoolId: string, lat: number, lng: number) => void;
  onBookCorridorSeat: (tripId: string, learnerId: string, lat: number, lng: number) => void;
  onSearchOffPeak: (lat: number, lng: number) => void;
}

export function CorridorSearchScreen({
  learners,
  schools,
  corridorOptions,
  offPeakDrivers,
  onSearchCorridor,
  onBookCorridorSeat,
  onSearchOffPeak,
}: CorridorSearchScreenProps): React.JSX.Element {
  const [selectedLearnerId, setSelectedLearnerId] = useState(learners[0]?.learnerId ?? '');
  const [selectedSchoolId, setSelectedSchoolId] = useState(
    learners[0]?.schoolId ?? schools[0]?.id ?? '',
  );
  const [pickupLat, setPickupLat] = useState(String(learners[0]?.homeLatitude ?? -26.246));
  const [pickupLng, setPickupLng] = useState(String(learners[0]?.homeLongitude ?? 27.912));

  const effectiveLearnerId = selectedLearnerId || learners[0]?.learnerId || '';
  const effectiveSchoolId = selectedSchoolId || learners[0]?.schoolId || schools[0]?.id || '';
  const peakNow = isPeakWindowSast();

  return (
    <ScrollView style={styles.container} contentContainerStyle={styles.content}>
      <Text style={styles.heading}>1.5 km Spatial Corridor Booking</Text>
      <Text style={styles.subheading}>
        SAST Window: {peakNow ? 'Peak Corridor Mode (Rule A Active)' : 'Off-Peak Window (Corridor & Point-to-Point Available)'}
      </Text>

      <View style={styles.searchCard}>
        <Text style={styles.label}>Select Learner</Text>
        {learners.map(l => (
          <TouchableOpacity
            key={l.learnerId}
            style={[styles.chipOption, effectiveLearnerId === l.learnerId && styles.chipSelected]}
            onPress={() => {
              setSelectedLearnerId(l.learnerId);
              setSelectedSchoolId(l.schoolId);
              setPickupLat(String(l.homeLatitude));
              setPickupLng(String(l.homeLongitude));
            }}>
            <Text style={styles.chipText}>{l.fullName} ({l.grade})</Text>
          </TouchableOpacity>
        ))}

        <View style={styles.row}>
          <View style={styles.half}>
            <Text style={styles.label}>Pickup Latitude</Text>
            <TextInput style={styles.input} value={pickupLat} onChangeText={setPickupLat} />
          </View>
          <View style={styles.half}>
            <Text style={styles.label}>Pickup Longitude</Text>
            <TextInput style={styles.input} value={pickupLng} onChangeText={setPickupLng} />
          </View>
        </View>

        <View style={styles.buttonRow}>
          <TouchableOpacity
            style={[styles.primaryBtn, !effectiveSchoolId && styles.disabled]}
            disabled={!effectiveSchoolId}
            onPress={() =>
              onSearchCorridor(effectiveSchoolId, Number(pickupLat) || -26.246, Number(pickupLng) || 27.912)
            }>
            <Text style={styles.primaryBtnText}>Find 1.5 km Routes</Text>
          </TouchableOpacity>

          <TouchableOpacity
            style={styles.secondaryBtn}
            onPress={() =>
              onSearchOffPeak(Number(pickupLat) || -26.246, Number(pickupLng) || 27.912)
            }>
            <Text style={styles.secondaryBtnText}>Off-Peak (5 km)</Text>
          </TouchableOpacity>
        </View>
      </View>

      <Text style={styles.sectionTitle}>Corridor Seat Matches ({corridorOptions.length})</Text>
      {corridorOptions.map(opt => (
        <View key={opt.tripId} style={styles.resultCard}>
          <View style={styles.topRow}>
            <Text style={styles.routeTitle}>{opt.routeName}</Text>
            {opt.hasSgbEndorsement ? (
              <Text style={styles.sgbBadge}>SGB Endorsed</Text>
            ) : null}
          </View>
          <Text style={styles.meta}>
            {opt.driverFullName} • {opt.vehicleMakeModel} ({opt.vehicleRegistration})
          </Text>
          <Text style={styles.distanceText}>
            {formatDistanceMeters(opt.distanceMeters)} from corridor • {opt.availableCorridorSeats} empty seat(s)
          </Text>
          {effectiveLearnerId ? (
            <TouchableOpacity
              style={styles.bookBtn}
              onPress={() =>
                onBookCorridorSeat(
                  opt.tripId,
                  effectiveLearnerId,
                  Number(pickupLat) || -26.246,
                  Number(pickupLng) || 27.912,
                )
              }>
              <Text style={styles.bookBtnText}>Book Empty Corridor Seat</Text>
            </TouchableOpacity>
          ) : null}
        </View>
      ))}

      {offPeakDrivers.length > 0 ? (
        <>
          <Text style={styles.sectionTitle}>Nearby Off-Peak Compliant Drivers ({offPeakDrivers.length})</Text>
          {offPeakDrivers.map(drv => (
            <View key={drv.driverProfileId} style={styles.resultCard}>
              <Text style={styles.routeTitle}>{drv.driverFullName}</Text>
              <Text style={styles.meta}>
                {drv.vehicleMakeModel} ({drv.vehicleRegistration}) • {formatDistanceMeters(drv.distanceMeters)} away
              </Text>
            </View>
          ))}
        </>
      ) : null}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#F5F7FA' },
  content: { padding: 16, gap: 12 },
  heading: { fontSize: 20, fontWeight: '700', color: '#0F172A' },
  subheading: { fontSize: 12, color: '#0369A1', fontWeight: '600' },
  searchCard: {
    backgroundColor: '#FFFFFF',
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: '#CBD5E1',
  },
  label: { fontSize: 12, fontWeight: '600', color: '#334155', marginTop: 8, marginBottom: 4 },
  chipOption: {
    padding: 8,
    borderWidth: 1,
    borderColor: '#E2E8F0',
    borderRadius: 6,
    marginBottom: 4,
  },
  chipSelected: { borderColor: '#1D4ED8', backgroundColor: '#EFF6FF' },
  chipText: { fontSize: 13, fontWeight: '600' },
  row: { flexDirection: 'row', gap: 10 },
  half: { flex: 1 },
  input: {
    borderWidth: 1,
    borderColor: '#CBD5E1',
    borderRadius: 8,
    paddingHorizontal: 10,
    paddingVertical: 8,
    backgroundColor: '#F8FAFC',
  },
  buttonRow: { flexDirection: 'row', gap: 8, marginTop: 12 },
  primaryBtn: {
    flex: 1,
    backgroundColor: '#1D4ED8',
    borderRadius: 8,
    paddingVertical: 11,
    alignItems: 'center',
  },
  disabled: { opacity: 0.5 },
  primaryBtnText: { color: '#FFFFFF', fontWeight: '700', fontSize: 13 },
  secondaryBtn: {
    flex: 1,
    borderWidth: 1,
    borderColor: '#1D4ED8',
    borderRadius: 8,
    paddingVertical: 11,
    alignItems: 'center',
  },
  secondaryBtnText: { color: '#1D4ED8', fontWeight: '700', fontSize: 13 },
  sectionTitle: { fontSize: 15, fontWeight: '700', color: '#0F172A', marginTop: 4 },
  resultCard: {
    backgroundColor: '#FFFFFF',
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: '#E2E8F0',
    gap: 4,
  },
  topRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  routeTitle: { fontSize: 15, fontWeight: '700', color: '#0F172A' },
  sgbBadge: {
    backgroundColor: '#D1FAE5',
    color: '#065F46',
    fontSize: 11,
    fontWeight: '700',
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 6,
  },
  meta: { fontSize: 12, color: '#64748B' },
  distanceText: { fontSize: 13, fontWeight: '600', color: '#1D4ED8' },
  bookBtn: {
    marginTop: 8,
    backgroundColor: '#059669',
    borderRadius: 8,
    paddingVertical: 10,
    alignItems: 'center',
  },
  bookBtnText: { color: '#FFFFFF', fontWeight: '700', fontSize: 13 },
});
