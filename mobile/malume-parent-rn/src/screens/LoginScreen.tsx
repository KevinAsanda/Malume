import React, { useState } from 'react';
import {
  ActivityIndicator,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import { DEFAULT_API_BASE_URL } from '../services/apiClient';

interface LoginScreenProps {
  initialBaseUrl?: string;
  isLoading: boolean;
  errorMessage: string | null;
  onLogin: (baseUrl: string, email: string, password: string) => void;
  onRegister: (
    baseUrl: string,
    email: string,
    fullName: string,
    phoneNumber: string,
    password: string,
  ) => void;
}

export function LoginScreen({
  initialBaseUrl = DEFAULT_API_BASE_URL,
  isLoading,
  errorMessage,
  onLogin,
  onRegister,
}: LoginScreenProps): React.JSX.Element {
  const [isRegisterMode, setIsRegisterMode] = useState(false);
  const [baseUrl, setBaseUrl] = useState(initialBaseUrl);
  const [email, setEmail] = useState('');
  const [fullName, setFullName] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('+2782');
  const [password, setPassword] = useState('');

  const handleSubmit = () => {
    if (isRegisterMode) {
      onRegister(baseUrl, email, fullName, phoneNumber, password);
    } else {
      onLogin(baseUrl, email, password);
    }
  };

  const canSubmit =
    !isLoading &&
    email.trim().length > 0 &&
    password.length >= 6 &&
    (!isRegisterMode || (fullName.trim().length > 0 && phoneNumber.trim().length >= 8));

  return (
    <View style={styles.container}>
      <Text style={styles.brand}>Malume Parent</Text>
      <Text style={styles.subtitle}>
        Scholar corridor seats, live GPS tracking &amp; daily rotating PIN
      </Text>

      <Text style={styles.label}>API Base URL</Text>
      <TextInput
        style={styles.input}
        value={baseUrl}
        onChangeText={setBaseUrl}
        autoCapitalize="none"
        placeholder="http://10.0.2.2:5144"
      />

      {isRegisterMode && (
        <>
          <Text style={styles.label}>Parent Full Name</Text>
          <TextInput
            style={styles.input}
            value={fullName}
            onChangeText={setFullName}
            placeholder="Thandi Mokoena"
          />

          <Text style={styles.label}>Mobile Number</Text>
          <TextInput
            style={styles.input}
            value={phoneNumber}
            onChangeText={setPhoneNumber}
            keyboardType="phone-pad"
            placeholder="+27821234567"
          />
        </>
      )}

      <Text style={styles.label}>Email</Text>
      <TextInput
        style={styles.input}
        value={email}
        onChangeText={setEmail}
        autoCapitalize="none"
        keyboardType="email-address"
        placeholder="parent@malume.co.za"
      />

      <Text style={styles.label}>Password</Text>
      <TextInput
        style={styles.input}
        value={password}
        onChangeText={setPassword}
        secureTextEntry
        placeholder="Password"
      />

      {errorMessage ? (
        <View style={styles.errorBox}>
          <Text style={styles.errorText}>{errorMessage}</Text>
        </View>
      ) : null}

      <TouchableOpacity
        style={[styles.primaryButton, !canSubmit && styles.disabledButton]}
        disabled={!canSubmit}
        onPress={handleSubmit}>
        {isLoading ? (
          <ActivityIndicator color="#FFFFFF" />
        ) : (
          <Text style={styles.primaryButtonText}>
            {isRegisterMode ? 'Create Parent Account' : 'Sign In'}
          </Text>
        )}
      </TouchableOpacity>

      <TouchableOpacity
        style={styles.toggleLink}
        onPress={() => setIsRegisterMode(prev => !prev)}>
        <Text style={styles.toggleLinkText}>
          {isRegisterMode
            ? 'Already have an account? Sign In'
            : 'New parent? Register an account'}
        </Text>
      </TouchableOpacity>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    justifyContent: 'center',
    padding: 24,
    backgroundColor: '#F5F7FA',
  },
  brand: {
    fontSize: 28,
    fontWeight: '700',
    color: '#0F172A',
  },
  subtitle: {
    fontSize: 14,
    color: '#475569',
    marginTop: 4,
    marginBottom: 20,
  },
  label: {
    fontSize: 12,
    fontWeight: '600',
    color: '#334155',
    marginTop: 10,
    marginBottom: 4,
  },
  input: {
    backgroundColor: '#FFFFFF',
    borderWidth: 1,
    borderColor: '#CBD5E1',
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 15,
    color: '#0F172A',
  },
  errorBox: {
    backgroundColor: '#FEE2E2',
    borderRadius: 8,
    padding: 10,
    marginTop: 12,
  },
  errorText: {
    color: '#991B1B',
    fontSize: 13,
  },
  primaryButton: {
    backgroundColor: '#1D4ED8',
    borderRadius: 8,
    paddingVertical: 14,
    alignItems: 'center',
    marginTop: 20,
  },
  disabledButton: {
    opacity: 0.5,
  },
  primaryButtonText: {
    color: '#FFFFFF',
    fontSize: 16,
    fontWeight: '700',
  },
  toggleLink: {
    marginTop: 16,
    alignItems: 'center',
  },
  toggleLinkText: {
    color: '#1D4ED8',
    fontSize: 14,
    fontWeight: '600',
  },
});
