import React, { useState } from 'react';
import {
  Modal,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import {
  CreateLearnerRequest,
  DailyPinDto,
  LearnerDto,
  LearnerQrBadgeDto,
  SchoolDto,
} from '../types/api';

interface LearnersScreenProps {
  learners: LearnerDto[];
  schools: SchoolDto[];
  activePass: {
    learnerName: string;
    qr?: LearnerQrBadgeDto;
    pin?: DailyPinDto;
  } | null;
  onCreateLearner: (payload: CreateLearnerRequest) => void;
  onLoadSecurityPass: (learnerId: string, direction: 'MorningToSchool' | 'AfternoonFromSchool') => void;
  onClosePass: () => void;
}

export function LearnersScreen({
  learners,
  schools,
  activePass,
  onCreateLearner,
  onLoadSecurityPass,
  onClosePass,
}: LearnersScreenProps): React.JSX.Element {
  const [showForm, setShowForm] = useState(false);
  const [selectedSchoolId, setSelectedSchoolId] = useState(schools[0]?.id ?? '');
  const [fullName, setFullName] = useState('');
  const [grade, setGrade] = useState('Grade 5');
  const [address, setAddress] = useState('42 Vilakazi St, Orlando West');
  const [latText, setLatText] = useState('-26.2450');
  const [lngText, setLngText] = useState('27.9082');
  const [emergencyPhone, setEmergencyPhone] = useState('+27821234567');
  const [pinDirection, setPinDirection] = useState<'MorningToSchool' | 'AfternoonFromSchool'>(
    'MorningToSchool',
  );

  const effectiveSchoolId = selectedSchoolId || schools[0]?.id || '';

  const handleAdd = () => {
    if (!effectiveSchoolId || !fullName.trim()) {
      return;
    }
    onCreateLearner({
      schoolId: effectiveSchoolId,
      fullName: fullName.trim(),
      grade: grade.trim() || 'Grade 1',
      homeLatitude: Number(latText) || -26.245,
      homeLongitude: Number(lngText) || 27.9082,
      homeAddressText: address.trim(),
      emergencyContactPhone: emergencyPhone.trim(),
    });
    setFullName('');
    setShowForm(false);
  };

  return (
    <ScrollView style={styles.container} contentContainerStyle={styles.content}>
      <View style={styles.headerRow}>
        <Text style={styles.heading}>My Learners &amp; Handover PIN</Text>
        <TouchableOpacity style={styles.addBtn} onPress={() => setShowForm(prev => !prev)}>
          <Text style={styles.addBtnText}>{showForm ? 'Close' : '+ Add Learner'}</Text>
        </TouchableOpacity>
      </View>

      {showForm && (
        <View style={styles.formCard}>
          <Text style={styles.formTitle}>Register Learner</Text>
          <Text style={styles.label}>School ({schools.length} available)</Text>
          {schools.map(s => (
            <TouchableOpacity
              key={s.id}
              style={[styles.schoolOption, effectiveSchoolId === s.id && styles.schoolSelected]}
              onPress={() => setSelectedSchoolId(s.id)}>
              <Text style={styles.schoolText}>{s.name}</Text>
            </TouchableOpacity>
          ))}

          <Text style={styles.label}>Learner Full Name</Text>
          <TextInput style={styles.input} value={fullName} onChangeText={setFullName} />
          <Text style={styles.label}>Grade</Text>
          <TextInput style={styles.input} value={grade} onChangeText={setGrade} />
          <Text style={styles.label}>Home Pickup Address</Text>
          <TextInput style={styles.input} value={address} onChangeText={setAddress} />
          <View style={styles.row}>
            <View style={styles.half}>
              <Text style={styles.label}>Pickup Lat</Text>
              <TextInput style={styles.input} value={latText} onChangeText={setLatText} />
            </View>
            <View style={styles.half}>
              <Text style={styles.label}>Pickup Lng</Text>
              <TextInput style={styles.input} value={lngText} onChangeText={setLngText} />
            </View>
          </View>
          <Text style={styles.label}>Emergency Contact Phone</Text>
          <TextInput style={styles.input} value={emergencyPhone} onChangeText={setEmergencyPhone} />

          <TouchableOpacity style={styles.saveBtn} onPress={handleAdd}>
            <Text style={styles.saveBtnText}>Save Learner</Text>
          </TouchableOpacity>
        </View>
      )}

      {learners.map(learner => (
        <View key={learner.learnerId} style={styles.card}>
          <Text style={styles.learnerName}>
            {learner.fullName} ({learner.grade})
          </Text>
          <Text style={styles.meta}>
            {learner.homeAddressText} • Emergency: {learner.emergencyContactPhone}
          </Text>
          <TouchableOpacity
            style={styles.passBtn}
            onPress={() => onLoadSecurityPass(learner.learnerId, pinDirection)}>
            <Text style={styles.passBtnText}>View Signed QR Badge &amp; Today's 4-Digit PIN</Text>
          </TouchableOpacity>
        </View>
      ))}

      <Modal visible={activePass !== null} transparent animationType="slide">
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={styles.modalTitle}>{activePass?.learnerName} — Handover Pass</Text>
            <View style={styles.dirRow}>
              {(['MorningToSchool', 'AfternoonFromSchool'] as const).map(dir => (
                <TouchableOpacity
                  key={dir}
                  style={[styles.dirChip, pinDirection === dir && styles.dirChipActive]}
                  onPress={() => {
                    setPinDirection(dir);
                    if (activePass?.pin?.learnerId) {
                      onLoadSecurityPass(activePass.pin.learnerId, dir);
                    }
                  }}>
                  <Text style={styles.dirText}>{dir === 'MorningToSchool' ? 'Morning' : 'Afternoon'}</Text>
                </TouchableOpacity>
              ))}
            </View>

            <View style={styles.pinBox}>
              <Text style={styles.pinLabel}>Daily Rotating Fallback PIN ({activePass?.pin?.serviceDate})</Text>
              <Text style={styles.pinDigits}>{activePass?.pin?.pinCode ?? '----'}</Text>
            </View>

            <Text style={styles.label}>Signed Cryptographic QR Payload (ECDSA P-256)</Text>
            <Text selectable style={styles.qrPayloadBox}>
              {activePass?.qr?.qrPayload ?? 'Loading...'}
            </Text>

            <TouchableOpacity style={styles.closeModalBtn} onPress={onClosePass}>
              <Text style={styles.closeModalText}>Done</Text>
            </TouchableOpacity>
          </View>
        </View>
      </Modal>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#F5F7FA' },
  content: { padding: 16, gap: 12 },
  headerRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  heading: { fontSize: 20, fontWeight: '700', color: '#0F172A' },
  addBtn: { backgroundColor: '#1D4ED8', borderRadius: 8, paddingHorizontal: 12, paddingVertical: 8 },
  addBtnText: { color: '#FFFFFF', fontWeight: '700', fontSize: 13 },
  formCard: {
    backgroundColor: '#FFFFFF',
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: '#CBD5E1',
  },
  formTitle: { fontSize: 16, fontWeight: '700', marginBottom: 8 },
  label: { fontSize: 12, fontWeight: '600', color: '#334155', marginTop: 8, marginBottom: 4 },
  input: {
    borderWidth: 1,
    borderColor: '#CBD5E1',
    borderRadius: 8,
    paddingHorizontal: 10,
    paddingVertical: 8,
    backgroundColor: '#F8FAFC',
  },
  row: { flexDirection: 'row', gap: 10 },
  half: { flex: 1 },
  schoolOption: {
    padding: 8,
    borderWidth: 1,
    borderColor: '#E2E8F0',
    borderRadius: 6,
    marginBottom: 4,
  },
  schoolSelected: { borderColor: '#1D4ED8', backgroundColor: '#EFF6FF' },
  schoolText: { fontSize: 13, fontWeight: '600' },
  saveBtn: {
    backgroundColor: '#059669',
    borderRadius: 8,
    paddingVertical: 12,
    alignItems: 'center',
    marginTop: 12,
  },
  saveBtnText: { color: '#FFFFFF', fontWeight: '700' },
  card: {
    backgroundColor: '#FFFFFF',
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: '#E2E8F0',
    gap: 6,
  },
  learnerName: { fontSize: 16, fontWeight: '700', color: '#0F172A' },
  meta: { fontSize: 12, color: '#64748B' },
  passBtn: {
    backgroundColor: '#EFF6FF',
    borderRadius: 8,
    paddingVertical: 10,
    alignItems: 'center',
    marginTop: 4,
  },
  passBtnText: { color: '#1D4ED8', fontWeight: '700', fontSize: 13 },
  modalBackdrop: {
    flex: 1,
    backgroundColor: 'rgba(15, 23, 42, 0.55)',
    justifyContent: 'center',
    padding: 20,
  },
  modalCard: { backgroundColor: '#FFFFFF', borderRadius: 14, padding: 18, gap: 10 },
  modalTitle: { fontSize: 18, fontWeight: '700', color: '#0F172A' },
  dirRow: { flexDirection: 'row', gap: 8 },
  dirChip: {
    flex: 1,
    paddingVertical: 8,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#CBD5E1',
    alignItems: 'center',
  },
  dirChipActive: { backgroundColor: '#DBEAFE', borderColor: '#1D4ED8' },
  dirText: { fontWeight: '600', fontSize: 13 },
  pinBox: {
    backgroundColor: '#0F172A',
    borderRadius: 10,
    padding: 14,
    alignItems: 'center',
  },
  pinLabel: { color: '#94A3B8', fontSize: 12 },
  pinDigits: { color: '#FFFFFF', fontSize: 34, fontWeight: '800', letterSpacing: 8, marginTop: 4 },
  qrPayloadBox: {
    backgroundColor: '#F1F5F9',
    borderRadius: 8,
    padding: 10,
    fontSize: 11,
    color: '#0F172A',
  },
  closeModalBtn: {
    backgroundColor: '#1D4ED8',
    borderRadius: 8,
    paddingVertical: 12,
    alignItems: 'center',
    marginTop: 4,
  },
  closeModalText: { color: '#FFFFFF', fontWeight: '700' },
});
