import React, { useEffect, useState } from 'react';
import {
  Linking,
  ScrollView,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import {
  InitializeSubscriptionPaymentResponse,
  LearnerDto,
  ScheduledRouteDto,
  SubscriptionEscrowDto,
} from '../types/api';

interface SubscriptionsScreenProps {
  learners: LearnerDto[];
  routes: ScheduledRouteDto[];
  subscriptions: SubscriptionEscrowDto[];
  pendingCheckout: InitializeSubscriptionPaymentResponse | null;
  onInitializePayment: (routeId: string, learnerId: string) => void;
  onVerifyPayment: (reference: string) => void;
}

export function SubscriptionsScreen({
  learners,
  routes,
  subscriptions,
  pendingCheckout,
  onInitializePayment,
  onVerifyPayment,
}: SubscriptionsScreenProps): React.JSX.Element {
  const [selectedLearnerId, setSelectedLearnerId] = useState<string>('');
  const [selectedRouteId, setSelectedRouteId] = useState<string>('');

  useEffect(() => {
    if (!selectedLearnerId && learners.length > 0) {
      setSelectedLearnerId(learners[0].learnerId);
    }
  }, [learners, selectedLearnerId]);

  useEffect(() => {
    if (!selectedRouteId && routes.length > 0) {
      setSelectedRouteId(routes[0].routeId);
    }
  }, [routes, selectedRouteId]);

  const activeRoutes = routes.filter(r => r.isActive);

  return (
    <ScrollView style={styles.container} contentContainerStyle={styles.content}>
      <Text style={styles.heading}>Paystack Checkout &amp; 40/30/30 Escrow</Text>

      <View style={styles.card}>
        <Text style={styles.sectionTitle}>Subscribe Learner to Monthly Route</Text>

        {learners.length === 0 ? (
          <Text style={styles.muted}>Register a learner first on the Learners &amp; PIN tab.</Text>
        ) : (
          <View style={styles.chipRow}>
            {learners.map(learner => (
              <TouchableOpacity
                key={learner.learnerId}
                style={[styles.chip, selectedLearnerId === learner.learnerId && styles.chipActive]}
                onPress={() => setSelectedLearnerId(learner.learnerId)}>
                <Text
                  style={[
                    styles.chipText,
                    selectedLearnerId === learner.learnerId && styles.chipTextActive,
                  ]}>
                  {learner.fullName}
                </Text>
              </TouchableOpacity>
            ))}
          </View>
        )}

        <Text style={styles.subLabel}>Select Scheduled Route</Text>
        {activeRoutes.length === 0 ? (
          <Text style={styles.muted}>No active scheduled routes published yet.</Text>
        ) : (
          <View style={styles.routeList}>
            {activeRoutes.map(route => {
              const selected = selectedRouteId === route.routeId;
              return (
                <TouchableOpacity
                  key={route.routeId}
                  style={[styles.routeCard, selected && styles.routeCardActive]}
                  onPress={() => setSelectedRouteId(route.routeId)}>
                  <View style={styles.routeHeader}>
                    <Text style={styles.routeName}>{route.routeName}</Text>
                    <Text style={styles.priceText}>R {route.monthlyPriceZar.toFixed(2)}/mo</Text>
                  </View>
                  <Text style={styles.meta}>
                    {route.direction} • {route.activeSubscribedSeats} subscribed seat(s)
                  </Text>
                </TouchableOpacity>
              );
            })}
          </View>
        )}

        <TouchableOpacity
          style={[
            styles.payBtn,
            (!selectedLearnerId || !selectedRouteId) && styles.payBtnDisabled,
          ]}
          disabled={!selectedLearnerId || !selectedRouteId}
          onPress={() => onInitializePayment(selectedRouteId, selectedLearnerId)}>
          <Text style={styles.payBtnText}>Pay with Paystack</Text>
        </TouchableOpacity>
      </View>

      {pendingCheckout ? (
        <View style={styles.checkoutCard}>
          <Text style={styles.checkoutTitle}>Paystack Checkout Ready</Text>
          <Text style={styles.meta}>Reference: {pendingCheckout.paystackReference}</Text>
          <Text style={styles.meta}>Amount: R {pendingCheckout.totalAmountZar.toFixed(2)}</Text>
          <View style={styles.actionRow}>
            <TouchableOpacity
              style={styles.openPaystackBtn}
              onPress={() => {
                void Linking.openURL(pendingCheckout.authorizationUrl);
              }}>
              <Text style={styles.openPaystackText}>Open Paystack Checkout</Text>
            </TouchableOpacity>
            <TouchableOpacity
              style={styles.verifyBtn}
              onPress={() => onVerifyPayment(pendingCheckout.paystackReference)}>
              <Text style={styles.verifyText}>Verify Payment</Text>
            </TouchableOpacity>
          </View>
        </View>
      ) : null}

      <Text style={styles.sectionTitle}>Active Escrow Subscriptions ({subscriptions.length})</Text>
      {subscriptions.length === 0 ? (
        <View style={styles.card}>
          <Text style={styles.muted}>No escrow subscriptions found yet.</Text>
        </View>
      ) : (
        subscriptions.map(sub => (
          <View key={sub.subscriptionId} style={styles.card}>
            <View style={styles.routeHeader}>
              <Text style={styles.routeName}>{sub.learnerFullName}</Text>
              <Text style={styles.statusPill}>{sub.escrowStatus}</Text>
            </View>
            <Text style={styles.meta}>
              {sub.routeName} • {sub.billingYearMonth} • R {sub.totalAmountZar.toFixed(2)}
            </Text>
            {sub.paystackReference ? (
              <Text style={styles.refText}>Paystack Ref: {sub.paystackReference}</Text>
            ) : null}
            <View style={styles.milestonesList}>
              {sub.milestones.map(m => (
                <View key={m.milestoneId} style={styles.milestoneRow}>
                  <Text style={styles.milestoneLabel}>
                    Tranche {m.trancheNumber} ({m.percentage}%) • {m.scheduledReleaseDate}
                  </Text>
                  <Text style={styles.milestoneAmount}>
                    R {m.amountZar.toFixed(2)} [{m.status}]
                  </Text>
                </View>
              ))}
            </View>
          </View>
        ))
      )}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#F5F7FA' },
  content: { padding: 16, gap: 12 },
  heading: { fontSize: 20, fontWeight: '700', color: '#0F172A' },
  sectionTitle: { fontSize: 15, fontWeight: '700', color: '#1E293B' },
  subLabel: { fontSize: 13, fontWeight: '600', color: '#334155', marginTop: 6 },
  card: {
    backgroundColor: '#FFFFFF',
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: '#E2E8F0',
    gap: 8,
  },
  muted: { color: '#64748B', fontSize: 13 },
  chipRow: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  chip: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 16,
    borderWidth: 1,
    borderColor: '#CBD5E1',
    backgroundColor: '#F8FAFC',
  },
  chipActive: { backgroundColor: '#1D4ED8', borderColor: '#1D4ED8' },
  chipText: { fontSize: 12, color: '#334155', fontWeight: '600' },
  chipTextActive: { color: '#FFFFFF' },
  routeList: { gap: 8 },
  routeCard: {
    borderWidth: 1,
    borderColor: '#CBD5E1',
    borderRadius: 10,
    padding: 10,
    backgroundColor: '#F8FAFC',
  },
  routeCardActive: { borderColor: '#0284C7', backgroundColor: '#F0F9FF' },
  routeHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  routeName: { fontSize: 14, fontWeight: '700', color: '#0F172A', flex: 1 },
  priceText: { fontSize: 13, fontWeight: '700', color: '#047857' },
  meta: { fontSize: 12, color: '#64748B' },
  refText: { fontSize: 11, color: '#475569' },
  payBtn: {
    backgroundColor: '#0D9488',
    borderRadius: 8,
    paddingVertical: 12,
    alignItems: 'center',
    marginTop: 4,
  },
  payBtnDisabled: { backgroundColor: '#94A3B8' },
  payBtnText: { color: '#FFFFFF', fontWeight: '700', fontSize: 14 },
  checkoutCard: {
    backgroundColor: '#ECFDF5',
    borderWidth: 1,
    borderColor: '#10B981',
    borderRadius: 12,
    padding: 14,
    gap: 6,
  },
  checkoutTitle: { fontSize: 15, fontWeight: '700', color: '#065F46' },
  actionRow: { flexDirection: 'row', gap: 8, marginTop: 6 },
  openPaystackBtn: {
    flex: 1,
    backgroundColor: '#047857',
    borderRadius: 8,
    paddingVertical: 10,
    alignItems: 'center',
  },
  openPaystackText: { color: '#FFFFFF', fontWeight: '700', fontSize: 13 },
  verifyBtn: {
    flex: 1,
    backgroundColor: '#1D4ED8',
    borderRadius: 8,
    paddingVertical: 10,
    alignItems: 'center',
  },
  verifyText: { color: '#FFFFFF', fontWeight: '700', fontSize: 13 },
  statusPill: {
    fontSize: 11,
    fontWeight: '700',
    color: '#1D4ED8',
    backgroundColor: '#EFF6FF',
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 6,
  },
  milestonesList: { gap: 4, marginTop: 4, borderTopWidth: 1, borderTopColor: '#F1F5F9', paddingTop: 6 },
  milestoneRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  milestoneLabel: { fontSize: 12, color: '#334155' },
  milestoneAmount: { fontSize: 12, fontWeight: '700', color: '#0F172A' },
});
