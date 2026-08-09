export const ControllerFamily = {
  Generic: 'Generic',
  PlayStation: 'PlayStation',
  Xbox: 'Xbox',
  Nintendo: 'Nintendo'
} as const;

export type ControllerFamily = typeof ControllerFamily[keyof typeof ControllerFamily];

const displayNames: Record<ControllerFamily, Record<string, string>> = {
  [ControllerFamily.Generic]: {},
  [ControllerFamily.PlayStation]: {
    South: 'Cross',
    East: 'Circle',
    West: 'Square',
    North: 'Triangle',
    Back: 'Share',
    Start: 'Options',
    Guide: 'PS',
    LeftShoulder: 'L1',
    RightShoulder: 'R1',
    LeftStick: 'L3',
    RightStick: 'R3',
    DpadUp: 'D-Pad Up',
    DpadDown: 'D-Pad Down',
    DpadLeft: 'D-Pad Left',
    DpadRight: 'D-Pad Right'
  },
  [ControllerFamily.Xbox]: {
    South: 'A',
    East: 'B',
    West: 'X',
    North: 'Y',
    Back: 'View',
    Start: 'Menu',
    Guide: 'Xbox',
    LeftShoulder: 'LB',
    RightShoulder: 'RB',
    LeftStick: 'LS',
    RightStick: 'RS',
    DpadUp: 'D-Pad Up',
    DpadDown: 'D-Pad Down',
    DpadLeft: 'D-Pad Left',
    DpadRight: 'D-Pad Right'
  },
  [ControllerFamily.Nintendo]: {
    South: 'B',
    East: 'A',
    West: 'Y',
    North: 'X',
    Back: '-',
    Start: '+',
    Guide: 'Home',
    LeftShoulder: 'L',
    RightShoulder: 'R',
    LeftStick: 'Left Stick',
    RightStick: 'Right Stick',
    DpadUp: 'D-Pad Up',
    DpadDown: 'D-Pad Down',
    DpadLeft: 'D-Pad Left',
    DpadRight: 'D-Pad Right'
  }
};

export function detectControllerFamily(deviceName: string | null | undefined): ControllerFamily {
  const normalizedName = deviceName?.toLowerCase() ?? '';
  if (/(dualsense|dualshock|playstation|sony)/i.test(normalizedName)) {
    return ControllerFamily.PlayStation;
  }

  if (/(xbox|xinput)/i.test(normalizedName)) {
    return ControllerFamily.Xbox;
  }

  if (/(nintendo|switch|pro controller|joy-con|joycon)/i.test(normalizedName)) {
    return ControllerFamily.Nintendo;
  }

  return ControllerFamily.Generic;
}

export function getControllerDisplayName(deviceName: string | null | undefined): string {
  const normalizedName = deviceName?.toLowerCase() ?? '';
  if (/dualsense/i.test(normalizedName)) {
    return 'DualSense';
  }

  if (detectControllerFamily(deviceName) === ControllerFamily.Xbox) {
    return 'Xbox Controller';
  }

  return 'Kontroler';
}

export function getControllerButtonDisplayName(button: string, family: ControllerFamily): string {
  return displayNames[family][button] ?? button;
}

export function mapControllerButtons(buttons: string[], family: ControllerFamily): string[] {
  return buttons.map((button) => getControllerButtonDisplayName(button, family));
}