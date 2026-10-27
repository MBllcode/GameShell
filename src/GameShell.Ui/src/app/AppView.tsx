import { DashboardPage, type DashboardPageProps } from '../features/dashboard/DashboardPage';
import { ProfileSetupPage, type ProfileSetupPageProps } from '../features/setup/ProfileSetupPage';
import { LoadingScreen } from '../shared/ui/LoadingScreen';
import type { BootstrapRoute } from '../features/setup/setupRouting';

type AppViewProps = {
  isInitialLoading: boolean;
  isHydrated: boolean;
  bootstrapRoute: BootstrapRoute;
  dashboard: DashboardPageProps;
  setup: ProfileSetupPageProps;
};

export function AppView({ isInitialLoading, isHydrated, bootstrapRoute, dashboard, setup }: AppViewProps) {
  if (isInitialLoading || !isHydrated || bootstrapRoute === 'Loading') {
    return <LoadingScreen />;
  }

  return bootstrapRoute === 'Dashboard' ? <DashboardPage {...dashboard} /> : <ProfileSetupPage {...setup} />;
}
