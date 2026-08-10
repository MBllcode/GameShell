import { DashboardPage, type DashboardPageProps } from './pages/DashboardPage';
import { ProfileSetupPage, type ProfileSetupPageProps } from './pages/ProfileSetupPage';
import { LoadingScreen } from './components/LoadingScreen';
import type { SetupMode } from './features/setup/setupFlow';
import type { BootstrapRoute } from './features/setup/setupRouting';

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
