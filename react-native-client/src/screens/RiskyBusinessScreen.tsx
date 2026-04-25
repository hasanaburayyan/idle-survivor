import React from 'react';
import { View, Text, TouchableOpacity, StyleSheet } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

export default function RiskyBusinessScreen({ navigation }: any) {
  return (
    <SafeAreaView style={styles.container}>
      <View style={styles.header}>
        <TouchableOpacity onPress={() => navigation.goBack()} style={styles.back}>
          <Text style={styles.backText}>← Back</Text>
        </TouchableOpacity>
        <Text style={styles.title}>RiskyBusiness</Text>
      </View>
      <View style={styles.content}>
        <Text style={styles.placeholder}>RiskyBusiness screen coming soon</Text>
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#0f0f0f' },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 16,
    paddingVertical: 12,
    borderBottomWidth: 1,
    borderBottomColor: '#2e2e2e',
    gap: 12,
  },
  back: { paddingVertical: 4 },
  backText: { color: '#c8a84b', fontSize: 14, fontWeight: '600' },
  title: { color: '#e0e0e0', fontSize: 18, fontWeight: '800', letterSpacing: 1 },
  content: { flex: 1, alignItems: 'center', justifyContent: 'center' },
  placeholder: { color: '#6b6b6b', fontSize: 14 },
});
