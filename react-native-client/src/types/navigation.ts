import type { NativeStackScreenProps } from '@react-navigation/native-stack';

export type RootStackParamList = {
  Login: undefined;
  Shelter: undefined;
  Character: undefined;
  Gear: undefined;
  SkillTree: undefined;
  Structures: undefined;
  Social: undefined;
  Adventure: undefined;
  RiskyBusiness: undefined;
};

export type LoginScreenProps = NativeStackScreenProps<RootStackParamList, 'Login'>;
export type ShelterScreenProps = NativeStackScreenProps<RootStackParamList, 'Shelter'>;
export type CharacterScreenProps = NativeStackScreenProps<RootStackParamList, 'Character'>;
export type GearScreenProps = NativeStackScreenProps<RootStackParamList, 'Gear'>;
export type SkillTreeScreenProps = NativeStackScreenProps<RootStackParamList, 'SkillTree'>;
export type StructuresScreenProps = NativeStackScreenProps<RootStackParamList, 'Structures'>;
export type SocialScreenProps = NativeStackScreenProps<RootStackParamList, 'Social'>;
export type AdventureScreenProps = NativeStackScreenProps<RootStackParamList, 'Adventure'>;
export type RiskyBusinessScreenProps = NativeStackScreenProps<RootStackParamList, 'RiskyBusiness'>;
