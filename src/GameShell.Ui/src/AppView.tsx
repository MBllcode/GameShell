import { DashboardPage, type DashboardPageProps } from './pages/DashboardPage';
import { ProfileSetupPage, type ProfileSetupPageProps } from './pages/ProfileSetupPage';
import { LoadingScreen } from './components/LoadingScreen';

type AppViewProps = {
  isInitialLoading: boolean;
  isSetupComplete: boolean;
  dashboard: DashboardPageProps;
  setup: ProfileSetupPageProps;
};

export function AppView({ isInitialLoading, isSetupComplete, dashboard, setup }: AppViewProps) {
  if (isInitialLoading) {
    return <LoadingScreen />;
  }

  return isSetupComplete
    ? <DashboardPage {...dashboard} />
    : <ProfileSetupPage {...setup} />;
}
