export interface UserProfile {
  id: string;
  keycloakId: string;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber?: string;
  birthDate?: string;
  role: string;
  avatarUrl?: string;
  createdAt: string;
  lastLogin?: string;
  isActive: boolean;
  aiProfileDataJson?: string;
  preferencesJson?: string;
}

export interface UpdateUserProfileDto {
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  birthDate?: string;
  avatarUrl?: string;
  preferencesJson?: string;
}
