export interface LogEntry {
  timestamp: string
  level: string
  source: string
  message: string
}

export interface ModuleInfo {
  id: string
  name: string
  path: string
  packageId?: string
  status: string  // 'Uninitialized' | 'Idle' | 'Active'
  active: boolean
  supportsEye: boolean
  supportsExpression: boolean
  crashCount: number
  retryCount: number
  lastMessage?: string
  recentMessages?: string[]
  isBuiltIn?: boolean
  enabled?: boolean
  hasConfig?: boolean
}

export interface TrackingData {
  eye: {
    left:  { gazeX: number; gazeY: number; openness: number; pupil: number }
    right: { gazeX: number; gazeY: number; openness: number; pupil: number }
  }
  shapes: number[]
  head: { yaw: number; pitch: number; roll: number; posX: number; posY: number; posZ: number }
}

export interface ParameterValue {
  name: string
  value: number
}

export interface CalibrationState {
  active: boolean
  progress: { name: string; min: number; max: number; current: number }[]
}

export interface OscStatus {
  connected: boolean
  ip: string
  port: number
  msgsPerSec: number
}

export interface AvatarInfo {
  name: string
  id: string
  paramCount: number
}

export interface AppConfig {
  debugMode: boolean
}

export interface OscConfig {
  ip: string
  sendPort: number
  recvPort: number
}

// V2 module config schema types (field names match C# PascalCase as sent over the wire)
export interface ConfigField {
  Key: string
  Label: string
  Description?: string
  Type: 'Float' | 'Int' | 'Bool' | 'String' | 'Enum' | 'FilePath'
  DefaultValue: unknown
  Min?: number
  Max?: number
  Options?: string[]
}

export interface ConfigSchema {
  Fields: ConfigField[]
}

export interface ModuleConfigPayload {
  moduleId: string
  schema: ConfigSchema
  values: Record<string, unknown> | null
}

export interface RegistryModule {
  packageId: string
  displayName: string
  author: string
  description: string
  version: string
  downloadUrl?: string
  installedVersion?: string
  installState: string   // 'NotInstalled' | 'Installing' | 'Installed' | 'UpdateAvailable' | 'Error'
  installProgress?: number
  usesEye: boolean
  usesExpression: boolean
  tags?: string[]
  pageUrl?: string
  usageInstructions?: string
  iconUrl?: string
}
