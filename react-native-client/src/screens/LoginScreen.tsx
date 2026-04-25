import React, { useEffect, useRef, useState } from 'react';
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  ActivityIndicator,
  Animated,
  StyleSheet,
  KeyboardAvoidingView,
  Platform,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import type { LoginScreenProps } from '../types/navigation';
import {
  initConnection,
  useConnectionStatus,
  reducers,
  clearToken,
} from '../spacetime/client';
import { usePlayerStore } from '../stores';

type Screen = 'connecting' | 'signup' | 'error';

export default function LoginScreen({ navigation }: LoginScreenProps) {
  const { status, identity, error } = useConnectionStatus();
  const player = usePlayerStore(s => s.player);

  const [screen, setScreen] = useState<Screen>('connecting');
  const [name, setName] = useState('');
  const [nameError, setNameError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const pulseAnim = useRef(new Animated.Value(1)).current;

  // Pulse animation for logo
  useEffect(() => {
    Animated.loop(
      Animated.sequence([
        Animated.timing(pulseAnim, { toValue: 1.04, duration: 2000, useNativeDriver: true }),
        Animated.timing(pulseAnim, { toValue: 1,    duration: 2000, useNativeDriver: true }),
      ])
    ).start();
  }, [pulseAnim]);

  // Kick off connection on mount
  useEffect(() => {
    initConnection();
  }, []);

  // React to connection state changes
  useEffect(() => {
    if (status === 'error') {
      setScreen('error');
      return;
    }
    if (status === 'connecting' || status === 'connected') {
      // Still loading — 'connected' means WebSocket is up but subscription
      // snapshot hasn't arrived yet. Keep showing the spinner.
      setScreen('connecting');
      return;
    }
    if (status === 'subscribed') {
      if (player) {
        // Returning player — subscription snapshot included their row
        navigation.replace('Shelter');
      } else {
        // New identity — no player row in snapshot, show signup
        setScreen('signup');
      }
    }
  }, [status, player, navigation]);

  // Once player row arrives after signup, navigate
  useEffect(() => {
    if (player && (status === 'connected' || status === 'subscribed')) {
      navigation.replace('Shelter');
    }
  }, [player, status, navigation]);

  function handleCreate() {
    const trimmed = name.trim();
    if (trimmed.length < 3) {
      setNameError('Name must be at least 3 characters.');
      return;
    }
    if (trimmed.length > 20) {
      setNameError('Name must be 20 characters or fewer.');
      return;
    }
    setNameError('');
    setSubmitting(true);
    // CreatePlayer assigns a random name; SetName immediately renames them.
    reducers.createPlayer();
    // SetName will be called once the Player row lands in the store
    // (handled via the useEffect below).
    _pendingName = trimmed;
  }

  // Apply the chosen name once the player row arrives
  useEffect(() => {
    if (player && _pendingName) {
      reducers.setName(_pendingName);
      _pendingName = null;
    }
  }, [player]);

  async function handleStartFresh() {
    await clearToken();
    setScreen('connecting');
    initConnection(null);
  }

  return (
    <SafeAreaView style={styles.container}>
      <KeyboardAvoidingView
        style={styles.flex}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      >
        <View style={styles.content}>

          {/* Logo */}
          <Animated.View style={[styles.logo, { transform: [{ scale: pulseAnim }] }]}>
            <Text style={styles.logoTitle}>IDLE</Text>
            <Text style={styles.logoSubtitle}>SURVIVOR</Text>
            <View style={styles.divider} />
            <Text style={styles.tagline}>THE WASTELAND AWAITS</Text>
          </Animated.View>

          {/* ── Connecting ── */}
          {screen === 'connecting' && (
            <View style={styles.statusBox}>
              <ActivityIndicator color="#c8a84b" size="small" />
              <Text style={styles.statusText}>Connecting to the wasteland…</Text>
            </View>
          )}

          {/* ── Signup form ── */}
          {screen === 'signup' && (
            <View style={styles.formBox}>
              <Text style={styles.formTitle}>CHOOSE YOUR NAME</Text>
              <Text style={styles.formSubtitle}>
                This is how other survivors will know you.
              </Text>

              <TextInput
                style={[styles.input, nameError ? styles.inputError : null]}
                placeholder="Survivor name…"
                placeholderTextColor="#4a4a4a"
                value={name}
                onChangeText={t => { setName(t); setNameError(''); }}
                maxLength={20}
                autoCapitalize="words"
                autoCorrect={false}
                returnKeyType="done"
                onSubmitEditing={handleCreate}
                editable={!submitting}
              />

              {!!nameError && (
                <Text style={styles.errorText}>{nameError}</Text>
              )}

              <TouchableOpacity
                style={[styles.btn, submitting && styles.btnDisabled]}
                onPress={handleCreate}
                disabled={submitting}
              >
                {submitting
                  ? <ActivityIndicator color="#0f0f0f" size="small" />
                  : <Text style={styles.btnText}>ENTER THE WASTELAND</Text>
                }
              </TouchableOpacity>
            </View>
          )}

          {/* ── Error ── */}
          {screen === 'error' && (
            <View style={styles.statusBox}>
              <Text style={styles.errorHeading}>⚠ Connection failed</Text>
              <Text style={styles.errorDetail}>{error ?? 'Unknown error'}</Text>
              <TouchableOpacity
                style={[styles.btn, styles.btnOutline]}
                onPress={() => { setScreen('connecting'); initConnection(); }}
              >
                <Text style={styles.btnOutlineText}>RETRY</Text>
              </TouchableOpacity>
              <TouchableOpacity
                style={styles.freshLink}
                onPress={handleStartFresh}
              >
                <Text style={styles.freshLinkText}>Clear saved data &amp; start fresh</Text>
              </TouchableOpacity>
            </View>
          )}

          {/* Start fresh link (only when we have a saved identity) */}
          {screen === 'signup' && (
            <TouchableOpacity style={styles.freshLink} onPress={handleStartFresh}>
              <Text style={styles.freshLinkText}>Start with a new identity</Text>
            </TouchableOpacity>
          )}

        </View>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

// Module-level pending name — bridges the gap between form submit and player row arriving
let _pendingName: string | null = null;

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#0f0f0f' },
  flex: { flex: 1 },
  content: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'space-evenly',
    paddingHorizontal: 32,
    paddingVertical: 40,
  },

  // Logo
  logo: { alignItems: 'center' },
  logoTitle: {
    fontSize: 68,
    fontWeight: '900',
    color: '#c8a84b',
    letterSpacing: 14,
    textShadowColor: 'rgba(200,168,75,0.35)',
    textShadowOffset: { width: 0, height: 0 },
    textShadowRadius: 18,
  },
  logoSubtitle: {
    fontSize: 30,
    fontWeight: '700',
    color: '#e8c86a',
    letterSpacing: 10,
    marginTop: -6,
  },
  divider: {
    width: 100,
    height: 1,
    backgroundColor: '#c8a84b',
    marginVertical: 14,
    opacity: 0.4,
  },
  tagline: {
    fontSize: 10,
    color: '#6b6b6b',
    letterSpacing: 4,
    fontWeight: '600',
  },

  // Status box
  statusBox: {
    alignItems: 'center',
    gap: 12,
    paddingHorizontal: 8,
  },
  statusText: { color: '#6b6b6b', fontSize: 13, letterSpacing: 1 },

  // Form
  formBox: {
    width: '100%',
    gap: 12,
  },
  formTitle: {
    color: '#c8a84b',
    fontSize: 13,
    fontWeight: '800',
    letterSpacing: 4,
    textAlign: 'center',
    marginBottom: 4,
  },
  formSubtitle: {
    color: '#6b6b6b',
    fontSize: 13,
    textAlign: 'center',
    marginBottom: 8,
  },
  input: {
    backgroundColor: '#1e1e1e',
    borderWidth: 1,
    borderColor: '#2e2e2e',
    borderRadius: 6,
    paddingVertical: 14,
    paddingHorizontal: 16,
    color: '#e0e0e0',
    fontSize: 16,
    letterSpacing: 0.5,
  },
  inputError: {
    borderColor: '#c0392b',
  },

  // Buttons
  btn: {
    backgroundColor: '#c8a84b',
    borderRadius: 6,
    paddingVertical: 14,
    alignItems: 'center',
    marginTop: 4,
  },
  btnDisabled: {
    opacity: 0.5,
  },
  btnText: {
    color: '#0f0f0f',
    fontSize: 13,
    fontWeight: '900',
    letterSpacing: 3,
  },
  btnOutline: {
    backgroundColor: 'transparent',
    borderWidth: 1,
    borderColor: '#c8a84b',
    paddingHorizontal: 32,
    paddingVertical: 10,
    borderRadius: 4,
  },
  btnOutlineText: {
    color: '#c8a84b',
    fontSize: 12,
    fontWeight: '800',
    letterSpacing: 3,
  },

  // Errors
  errorText: { color: '#e74c3c', fontSize: 12, marginTop: -6 },
  errorHeading: { color: '#e74c3c', fontSize: 15, fontWeight: '700' },
  errorDetail: { color: '#6b6b6b', fontSize: 13, textAlign: 'center' },

  // Setup required
  setupText: {
    color: '#6b6b6b',
    fontSize: 13,
    textAlign: 'center',
    lineHeight: 20,
  },
  codeBlock: {
    backgroundColor: '#1e1e1e',
    borderRadius: 6,
    paddingVertical: 10,
    paddingHorizontal: 20,
    borderWidth: 1,
    borderColor: '#2e2e2e',
  },
  codeText: {
    color: '#c8a84b',
    fontFamily: Platform.OS === 'ios' ? 'Courier New' : 'monospace',
    fontSize: 14,
    letterSpacing: 1,
  },
  codeMono: {
    fontFamily: Platform.OS === 'ios' ? 'Courier New' : 'monospace',
    fontSize: 12,
    color: '#e0e0e0',
  },

  // Start fresh
  freshLink: { marginTop: 8, paddingVertical: 8 },
  freshLinkText: { color: '#3a3a3a', fontSize: 12, textDecorationLine: 'underline' },
});
