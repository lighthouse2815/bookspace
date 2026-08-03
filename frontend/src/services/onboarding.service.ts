import { api, unwrap } from '../lib/api'
import type { ApiEnvelope } from '../types/api'
import type { OnboardingState } from '../types/domain'

export interface SaveOnboardingPreferencesInput {
  preferredCategoryIds: string[]
  referenceBookIds: string[]
}

export const onboardingService = {
  state: async () =>
    unwrap(await api.get<ApiEnvelope<OnboardingState>>('/users/me/onboarding')),

  savePreferences: async (input: SaveOnboardingPreferencesInput) =>
    unwrap(await api.put<ApiEnvelope<OnboardingState>>('/users/me/onboarding', input)),

  complete: async () =>
    unwrap(await api.post<ApiEnvelope<OnboardingState>>('/users/me/onboarding/complete')),

  skip: async () =>
    unwrap(await api.post<ApiEnvelope<OnboardingState>>('/users/me/onboarding/skip')),
}
