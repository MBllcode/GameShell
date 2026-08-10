export type LauncherBrand = 'playnite' | 'steam' | 'xbox';

const launcherIcons: Record<LauncherBrand, string> = {
  playnite: './assets/launchers/playnite.svg',
  steam: './assets/launchers/steam.svg',
  xbox: './assets/launchers/xbox.svg'
};

export function LauncherIcon({ brand }: { brand: LauncherBrand }) {
  return <img className="launcher-icon" src={launcherIcons[brand]} alt="" aria-hidden="true" />;
}
