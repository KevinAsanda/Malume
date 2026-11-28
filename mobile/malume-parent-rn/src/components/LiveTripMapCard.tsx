import React from 'react';
import { Linking, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { DriverTelemetryPingDto, ParentActiveTripDto } from '../types/api';
import { formatDistanceMeters } from '../utils/formatters';
import {
  buildExternalMapUrl,
  haversineDistanceMeters,
  LatLngPoint,
  projectMapPoints,
} from '../utils/geoMap';

interface LiveTripMapCardProps {
  trip: ParentActiveTripDto;
  livePing?: DriverTelemetryPingDto;
  isSosActive: boolean;
}

interface PlottedPin extends LatLngPoint {
  id: 'home' | 'bus' | 'school';
  label: string;
  color: string;
}

export function LiveTripMapCard({
  trip,
  livePing,
  isSosActive,
}: LiveTripMapCardProps): React.JSX.Element {
  const driverLat = livePing?.latitude ?? trip.driverLatitude;
  const driverLng = livePing?.longitude ?? trip.driverLongitude;

  const pins: PlottedPin[] = [];
  if (trip.learnerHomeLatitude != null && trip.learnerHomeLongitude != null) {
    pins.push({
      id: 'home',
      label: 'Home',
      color: '#2563EB',
      latitude: trip.learnerHomeLatitude,
      longitude: trip.learnerHomeLongitude,
    });
  }

  if (driverLat != null && driverLng != null) {
    pins.push({
      id: 'bus',
      label: isSosActive || livePing?.isSosActive ? 'SOS' : 'Bus',
      color: isSosActive || livePing?.isSosActive ? '#DC2626' : '#16A34A',
      latitude: driverLat,
      longitude: driverLng,
    });
  }

  if (trip.schoolGateLatitude != null && trip.schoolGateLongitude != null) {
    pins.push({
      id: 'school',
      label: 'Gate',
      color: '#D97706',
      latitude: trip.schoolGateLatitude,
      longitude: trip.schoolGateLongitude,
    });
  }

  const projected = projectMapPoints(pins);

  const distToHome =
    driverLat != null &&
    driverLng != null &&
    trip.learnerHomeLatitude != null &&
    trip.learnerHomeLongitude != null
      ? haversineDistanceMeters(
          { latitude: driverLat, longitude: driverLng },
          { latitude: trip.learnerHomeLatitude, longitude: trip.learnerHomeLongitude },
        )
      : null;

  const distToGate =
    driverLat != null &&
    driverLng != null &&
    trip.schoolGateLatitude != null &&
    trip.schoolGateLongitude != null
      ? haversineDistanceMeters(
          { latitude: driverLat, longitude: driverLng },
          { latitude: trip.schoolGateLatitude, longitude: trip.schoolGateLongitude },
        )
      : null;

  const focusLat = driverLat ?? trip.learnerHomeLatitude ?? trip.schoolGateLatitude;
  const focusLng = driverLng ?? trip.learnerHomeLongitude ?? trip.schoolGateLongitude;

  const handleOpenExternalMap = () => {
    if (focusLat == null || focusLng == null) {
      return;
    }
    void Linking.openURL(buildExternalMapUrl(focusLat, focusLng, trip.vehicleRegistration));
  };

  return (
    <View style={styles.wrapper}>
      <View style={styles.mapHeader}>
        <Text style={styles.mapTitle}>Live Spatial Route Tracker</Text>
        {focusLat != null && focusLng != null ? (
          <TouchableOpacity onPress={handleOpenExternalMap}>
            <Text style={styles.openMapLink}>Open in Maps</Text>
          </TouchableOpacity>
        ) : null}
      </View>

      <View style={styles.canvas}>
        <View style={styles.gridRowTop} />
        <View style={styles.gridRowBottom} />
        <View style={styles.gridColMid} />

        {projected.length === 0 ? (
          <Text style={styles.awaitingText}>Awaiting GPS coordinates...</Text>
        ) : (
          projected.map(pin => (
            <View
              key={pin.id}
              style={[
                styles.pinWrapper,
                {
                  left: `${(pin.xPct * 100).toFixed(1)}%` as `${number}%`,
                  top: `${(pin.yPct * 100).toFixed(1)}%` as `${number}%`,
                },
              ]}>
              <View style={[styles.pinDot, { backgroundColor: pin.color }]} />
              <Text style={styles.pinLabel}>{pin.label}</Text>
            </View>
          ))
        )}
      </View>

      <View style={styles.metricsRow}>
        {distToHome != null ? (
          <Text style={styles.metricBadge}>To Home: {formatDistanceMeters(distToHome)}</Text>
        ) : null}
        {distToGate != null ? (
          <Text style={styles.metricBadge}>To Gate: {formatDistanceMeters(distToGate)}</Text>
        ) : null}
        {livePing ? (
          <Text style={styles.metricBadge}>{livePing.speedKph.toFixed(0)} km/h</Text>
        ) : null}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  wrapper: {
    marginTop: 8,
    backgroundColor: '#F8FAFC',
    borderWidth: 1,
    borderColor: '#CBD5E1',
    borderRadius: 10,
    padding: 10,
    gap: 8,
  },
  mapHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  mapTitle: {
    fontSize: 12,
    fontWeight: '700',
    color: '#1E293B',
  },
  openMapLink: {
    fontSize: 12,
    fontWeight: '700',
    color: '#1D4ED8',
  },
  canvas: {
    height: 140,
    backgroundColor: '#E2E8F0',
    borderRadius: 8,
    overflow: 'hidden',
    position: 'relative',
    justifyContent: 'center',
    alignItems: 'center',
  },
  gridRowTop: {
    position: 'absolute',
    left: 0,
    right: 0,
    top: '33%',
    height: 1,
    backgroundColor: '#CBD5E1',
  },
  gridRowBottom: {
    position: 'absolute',
    left: 0,
    right: 0,
    top: '66%',
    height: 1,
    backgroundColor: '#CBD5E1',
  },
  gridColMid: {
    position: 'absolute',
    top: 0,
    bottom: 0,
    left: '50%',
    width: 1,
    backgroundColor: '#CBD5E1',
  },
  awaitingText: {
    fontSize: 12,
    color: '#475569',
  },
  pinWrapper: {
    position: 'absolute',
    marginLeft: -18,
    marginTop: -12,
    alignItems: 'center',
    width: 36,
  },
  pinDot: {
    width: 14,
    height: 14,
    borderRadius: 7,
    borderWidth: 2,
    borderColor: '#FFFFFF',
  },
  pinLabel: {
    marginTop: 2,
    fontSize: 10,
    fontWeight: '700',
    color: '#0F172A',
    backgroundColor: 'rgba(255, 255, 255, 0.85)',
    paddingHorizontal: 4,
    borderRadius: 4,
  },
  metricsRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 6,
  },
  metricBadge: {
    fontSize: 11,
    fontWeight: '600',
    color: '#334155',
    backgroundColor: '#E0F2FE',
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 6,
  },
});
