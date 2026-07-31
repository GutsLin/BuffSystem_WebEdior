export const gameplayTagSchemaVersion = '1.0.0' as const;
export const gameplayTagSources = ['system', 'web'] as const;

export type GameplayTagSource = (typeof gameplayTagSources)[number];

export interface GameplayTagRecord {
  id: string;
  name: string;
  displayName: string;
  description: string;
  flags: number;
  source: GameplayTagSource;
  deprecated: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface GameplayTagsStorageData {
  version: number;
  publishedVersion: number;
  publishedAt: string | null;
  tags: GameplayTagRecord[];
}

export type GameplayTagExport = Omit<
  GameplayTagRecord,
  'id' | 'createdAt' | 'updatedAt'
>;

export interface GameplayTagsExportPayload {
  schemaVersion: typeof gameplayTagSchemaVersion;
  version: number;
  exportedAt: string;
  tags: GameplayTagExport[];
}
