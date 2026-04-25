import './global.css';

import React from 'react';
import { NavigationContainer } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { StatusBar } from 'expo-status-bar';

import type { RootStackParamList } from './src/types/navigation';

import LoginScreen         from './src/screens/LoginScreen';
import ShelterScreen       from './src/screens/ShelterScreen';
import CharacterScreen     from './src/screens/CharacterScreen';
import GearScreen          from './src/screens/GearScreen';
import SkillTreeScreen     from './src/screens/SkillTreeScreen';
import StructuresScreen    from './src/screens/StructuresScreen';
import SocialScreen        from './src/screens/SocialScreen';
import AdventureScreen     from './src/screens/AdventureScreen';
import RiskyBusinessScreen from './src/screens/RiskyBusinessScreen';

const Stack = createNativeStackNavigator<RootStackParamList>();

export default function App() {
  return (
    <SafeAreaProvider>
      <StatusBar style="light" backgroundColor="#0f0f0f" />
      <NavigationContainer
        theme={{
          dark: true,
          colors: {
            primary: '#c8a84b',
            background: '#0f0f0f',
            card: '#1a1a1a',
            text: '#e0e0e0',
            border: '#2e2e2e',
            notification: '#c0392b',
          },
          fonts: {
            regular: { fontFamily: 'System', fontWeight: '400' },
            medium:  { fontFamily: 'System', fontWeight: '500' },
            bold:    { fontFamily: 'System', fontWeight: '700' },
            heavy:   { fontFamily: 'System', fontWeight: '900' },
          },
        }}
      >
        <Stack.Navigator
          initialRouteName="Login"
          screenOptions={{
            headerShown: false,
            contentStyle: { backgroundColor: '#0f0f0f' },
            animation: 'fade',
          }}
        >
          <Stack.Screen name="Login"   component={LoginScreen} />
          <Stack.Screen name="Shelter" component={ShelterScreen} />
          <Stack.Screen
            name="Character"
            component={CharacterScreen}
            options={{ animation: 'slide_from_right' }}
          />
          <Stack.Screen
            name="Gear"
            component={GearScreen}
            options={{ animation: 'slide_from_right' }}
          />
          <Stack.Screen
            name="SkillTree"
            component={SkillTreeScreen}
            options={{ animation: 'slide_from_right' }}
          />
          <Stack.Screen
            name="Structures"
            component={StructuresScreen}
            options={{ animation: 'slide_from_right' }}
          />
          <Stack.Screen
            name="Social"
            component={SocialScreen}
            options={{ animation: 'slide_from_right' }}
          />
          <Stack.Screen
            name="Adventure"
            component={AdventureScreen}
            options={{ animation: 'slide_from_bottom' }}
          />
          <Stack.Screen
            name="RiskyBusiness"
            component={RiskyBusinessScreen}
            options={{ animation: 'slide_from_bottom' }}
          />
        </Stack.Navigator>
      </NavigationContainer>
    </SafeAreaProvider>
  );
}
