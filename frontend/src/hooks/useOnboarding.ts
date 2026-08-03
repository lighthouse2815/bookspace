import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../contexts/AuthContext'
import {
  onboardingService,
  type SaveOnboardingPreferencesInput,
} from '../services/onboarding.service'
import type { OnboardingState } from '../types/domain'
import { recommendationKeys } from './recommendationKeys'

export const onboardingKeys = {
  all: ['onboarding'] as const,
  principal: (principalId: string) => [...onboardingKeys.all, principalId] as const,
}

function useOnboardingScope() {
  const { user, isLoading } = useAuth()
  return { principalId: user?.id ?? 'guest', enabled: Boolean(user) && !isLoading }
}

async function invalidateOnboardingConsumers(
  queryClient: ReturnType<typeof useQueryClient>,
  principalId: string,
) {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: recommendationKeys.scoped(principalId) }),
    queryClient.invalidateQueries({ queryKey: ['library'] }),
    queryClient.invalidateQueries({ queryKey: ['people', principalId] }),
    queryClient.invalidateQueries({ queryKey: ['dashboard'] }),
    queryClient.invalidateQueries({ queryKey: ['reading-goals'] }),
  ])
}

export function useOnboarding() {
  const { principalId, enabled } = useOnboardingScope()
  return useQuery({
    queryKey: onboardingKeys.principal(principalId),
    queryFn: onboardingService.state,
    enabled,
    staleTime: 60_000,
  })
}

export function useSaveOnboardingPreferences() {
  const { principalId } = useOnboardingScope()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: SaveOnboardingPreferencesInput) =>
      onboardingService.savePreferences(input),
    onSuccess: async (state) => {
      queryClient.setQueryData<OnboardingState>(onboardingKeys.principal(principalId), state)
      await invalidateOnboardingConsumers(queryClient, principalId)
    },
  })
}

function useFinishOnboarding(action: 'complete' | 'skip') {
  const { principalId } = useOnboardingScope()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: action === 'complete' ? onboardingService.complete : onboardingService.skip,
    onSuccess: async (state) => {
      queryClient.setQueryData<OnboardingState>(onboardingKeys.principal(principalId), state)
      await invalidateOnboardingConsumers(queryClient, principalId)
    },
  })
}

export function useCompleteOnboarding() {
  return useFinishOnboarding('complete')
}

export function useSkipOnboarding() {
  return useFinishOnboarding('skip')
}
