import type {
  AttributeFormulaConfig,
  BuffPayload,
  BuffTemplate,
  GameplayTag,
  GameplayTagsExportPayload,
  GameplayTagsVersion,
  UnityExportPayload,
} from '@/types/buff';

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...init?.headers,
    },
  });

  if (!response.ok) {
    let message = `请求失败（${response.status}）`;
    try {
      const body = (await response.json()) as { message?: string | string[] };
      if (Array.isArray(body.message)) message = body.message.join('；');
      else if (body.message) message = body.message;
    } catch {
      // Keep the HTTP fallback message.
    }
    throw new Error(message);
  }

  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const api = {
  listBuffs: () => request<BuffTemplate[]>('/api/buffs'),
  getBuff: (id: string) => request<BuffTemplate>(`/api/buffs/${id}`),
  createBuff: (payload: BuffPayload) =>
    request<BuffTemplate>('/api/buffs', { method: 'POST', body: JSON.stringify(payload) }),
  updateBuff: (id: string, payload: BuffPayload) =>
    request<BuffTemplate>(`/api/buffs/${id}`, { method: 'PUT', body: JSON.stringify(payload) }),
  deleteBuff: (id: string) => request<void>(`/api/buffs/${id}`, { method: 'DELETE' }),
  duplicateBuff: (id: string) => request<BuffTemplate>(`/api/buffs/${id}/duplicate`, { method: 'POST' }),
  getAttributeFormula: () => request<AttributeFormulaConfig>('/api/settings/attribute-formula'),
  updateAttributeFormula: (payload: AttributeFormulaConfig) =>
    request<AttributeFormulaConfig>('/api/settings/attribute-formula', {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),
  getUnityPreview: () => request<UnityExportPayload>('/api/export/unity/preview'),
  listGameplayTags: () => request<GameplayTag[]>('/api/gameplay-tags'),
  getGameplayTagsVersion: () => request<GameplayTagsVersion>('/api/gameplay-tags/version'),
  createGameplayTag: (payload: Omit<GameplayTag, 'id' | 'createdAt' | 'updatedAt'>) =>
    request<GameplayTag>('/api/gameplay-tags', { method: 'POST', body: JSON.stringify(payload) }),
  updateGameplayTag: (id: string, payload: Omit<GameplayTag, 'id' | 'createdAt' | 'updatedAt'>) =>
    request<GameplayTag>(`/api/gameplay-tags/${id}`, { method: 'PUT', body: JSON.stringify(payload) }),
  publishGameplayTags: () => request<GameplayTagsExportPayload>('/api/gameplay-tags/publish', { method: 'POST' }),
  getGameplayTagsExport: () => request<GameplayTagsExportPayload>('/api/gameplay-tags/export'),
};
