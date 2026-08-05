import { Injectable, InternalServerErrorException, Logger } from '@nestjs/common';
import type { AttributeModifier, BuffTemplate, EffectAction } from '../common/buff.types';
import { GenerateBuffDto } from './dto/generate.dto';

type BuffPayload = Omit<BuffTemplate, 'id' | 'createdAt' | 'updatedAt'>;

interface DeepSeekMessage {
  role: 'system' | 'user' | 'assistant';
  content: string;
}

interface DeepSeekResponse {
  choices: Array<{ message: { content: string } }>;
}

@Injectable()
export class AiService {
  private readonly logger = new Logger(AiService.name);
  private readonly endpoint = 'https://api.deepseek.com/v1/chat/completions';

  async generate(dto: GenerateBuffDto): Promise<BuffPayload> {
    const apiKey = process.env['DEEPSEEK_API_KEY'];
    if (!apiKey) {
      throw new InternalServerErrorException('未配置 DEEPSEEK_API_KEY 环境变量');
    }

    const messages: DeepSeekMessage[] = [
      { role: 'system', content: this.buildSystemPrompt() },
      { role: 'user', content: dto.prompt },
    ];

    this.logger.log(`正在调用 DeepSeek API... prompt=${dto.prompt.slice(0, 80)}`);

    const response = await fetch(this.endpoint, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${apiKey}`,
      },
      body: JSON.stringify({
        model: 'deepseek-chat',
        messages,
        temperature: 0.3,
        max_tokens: 4096,
      }),
    });

    if (!response.ok) {
      const errorBody = await response.text().catch(() => 'unknown');
      this.logger.error(`DeepSeek API 返回错误 ${response.status}: ${errorBody}`);
      throw new InternalServerErrorException(`AI 服务请求失败（${response.status}）`);
    }

    const data = (await response.json()) as DeepSeekResponse;
    const rawContent = data.choices?.[0]?.message?.content ?? '';
    if (!rawContent) {
      throw new InternalServerErrorException('AI 返回内容为空');
    }

    const buff = this.parseBuffJson(rawContent);
    this.logger.log(`DeepSeek 生成成功: key=${buff.key}`);
    return buff;
  }

  private parseBuffJson(raw: string): BuffPayload {
    const jsonMatch = raw.match(/\{[\s\S]*\}/);
    if (!jsonMatch) {
      throw new InternalServerErrorException('AI 返回内容未包含有效 JSON');
    }

    let parsed: Record<string, unknown>;
    try {
      parsed = JSON.parse(jsonMatch[0]);
    } catch {
      throw new InternalServerErrorException('AI 返回 JSON 解析失败');
    }

    const result: Record<string, unknown> = {};

    result['key'] = this.sanitizeKey(String(parsed['key'] ?? parsed['Key'] ?? 'aiGenerated'));

    result['displayName'] =
      typeof parsed['displayName'] === 'string'
        ? parsed['displayName']
        : (typeof parsed['DisplayName'] === 'string' ? parsed['DisplayName'] : 'AI 生成效果');

    result['description'] =
      typeof parsed['description'] === 'string'
        ? parsed['description']
        : (typeof parsed['Description'] === 'string' ? parsed['Description'] : '');

    const validKinds = ['Buff', 'Debuff', 'Neutral'];
    const kind = String(parsed['modifierKind'] ?? parsed['ModifierKind'] ?? 'Buff');
    result['modifierKind'] = validKinds.includes(kind) ? kind : 'Buff';

    const validStacks = ['Refresh', 'Stack', 'Replace', 'Independent'];
    const stack = String(parsed['stackPolicy'] ?? parsed['StackPolicy'] ?? 'Refresh');
    result['stackPolicy'] = validStacks.includes(stack) ? stack : 'Refresh';

    result['duration'] = this.toNumber(parsed['duration'] ?? parsed['Duration'], 10);
    result['maxStacks'] = Math.max(1, this.toNumber(parsed['maxStacks'] ?? parsed['MaxStacks'], 1));
    result['thinkInterval'] = this.toNumber(parsed['thinkInterval'] ?? parsed['ThinkInterval'], -1);

    result['statusEffects'] = this.toStringArray(
      parsed['statusEffects'] ?? parsed['StatusEffects'] ?? [],
    );

    const validDispel = ['NotDispellable', 'BasicDispellable', 'StrongDispellable'];
    const dispel = String(parsed['dispelRule'] ?? parsed['DispelRule'] ?? 'BasicDispellable');
    result['dispelRule'] = validDispel.includes(dispel) ? dispel : 'BasicDispellable';

    result['affectedByStatusResistance'] =
      this.toBool(parsed['affectedByStatusResistance'] ?? parsed['AffectedByStatusResistance'], false);
    result['removeOnDeath'] = this.toBool(parsed['removeOnDeath'] ?? parsed['RemoveOnDeath'], true);
    result['isHidden'] = this.toBool(parsed['isHidden'] ?? parsed['IsHidden'], false);
    result['isPassive'] = this.toBool(parsed['isPassive'] ?? parsed['IsPassive'], false);
    result['isAura'] = this.toBool(parsed['isAura'] ?? parsed['IsAura'], false);

    result['auraRadius'] = Math.max(0, this.toNumber(parsed['auraRadius'] ?? parsed['AuraRadius'], 0));
    result['lingerDuration'] = Math.max(0, this.toNumber(parsed['lingerDuration'] ?? parsed['LingerDuration'], 0));

    result['attributeModifiers'] = this.parseAttributeModifiers(
      parsed['attributeModifiers'] ?? parsed['AttributeModifiers'] ?? [],
    );

    result['effectActions'] = this.parseEffectActions(
      parsed['effectActions'] ?? parsed['EffectActions'] ?? [],
    );

    result['tags'] = this.toStringArray(parsed['tags'] ?? parsed['Tags'] ?? []);

    result['applyChance'] = this.clamp(this.toNumber(parsed['applyChance'] ?? parsed['ApplyChance'], 1), 0, 1);
    result['applyCooldown'] = Math.max(0, this.toNumber(parsed['applyCooldown'] ?? parsed['ApplyCooldown'], 0));
    result['requiredStatusEffects'] = this.toStringArray(
      parsed['requiredStatusEffects'] ?? parsed['RequiredStatusEffects'] ?? [],
    );

    return result as BuffPayload;
  }

  private sanitizeKey(raw: string): string {
    let key = raw.replace(/[^A-Za-z0-9_]/g, '');
    if (key.length > 0 && /^\d/.test(key)) key = 'A' + key;
    if (!key) key = 'aiGenerated';
    if (key.length > 64) key = key.slice(0, 64);
    return key;
  }

  private toNumber(value: unknown, fallback: number): number {
    const n = Number(value);
    return Number.isFinite(n) ? n : fallback;
  }

  private toBool(value: unknown, fallback: boolean): boolean {
    if (typeof value === 'boolean') return value;
    if (typeof value === 'string') return value.toLowerCase() === 'true';
    return fallback;
  }

  private toStringArray(value: unknown): string[] {
    if (!Array.isArray(value)) return [];
    return value.map((v) => String(v).trim()).filter(Boolean);
  }

  private clamp(value: number, min: number, max: number): number {
    return Math.max(min, Math.min(max, value));
  }

  private parseAttributeModifiers(raw: unknown): BuffPayload['attributeModifiers'] {
    if (!Array.isArray(raw)) return [];
    return raw.map((item: Record<string, unknown>, index: number) => {
      const validOps = ['Add', 'PercentAdd', 'PercentMultiply', 'Override', 'Min', 'Max'];
      const op = String(item['op'] ?? item['Op'] ?? 'Add');
      return {
        id: String(item['id'] ?? item['Id'] ?? `ai-mod-${index}`),
        attributeType: String(item['attributeType'] ?? item['AttributeType'] ?? 'Strength'),
        op: validOps.includes(op) ? (op as AttributeModifier['op']) : 'Add',
        value: this.toNumber(item['value'] ?? item['Value'], 0),
        scaleByStacks: this.toBool(item['scaleByStacks'] ?? item['ScaleByStacks'], false),
        priority: this.toNumber(item['priority'] ?? item['Priority'], 0),
      };
    }) as unknown as BuffPayload['attributeModifiers'];
  }

  private parseEffectActions(raw: unknown): BuffPayload['effectActions'] {
    if (!Array.isArray(raw)) return [];
    return raw.map((item: Record<string, unknown>, index: number) => {
      const validTriggers = [
        'OnCreated', 'OnRefresh', 'OnStackChanged', 'OnIntervalThink',
        'OnAttackLanded', 'OnTakeDamage', 'OnDealDamage', 'OnDestroy', 'OnCustomEvent',
      ];
      const validActions = [
        'DealDamage', 'Heal', 'ModifyAttribute', 'ApplyModifier',
        'RemoveModifier', 'RefreshModifier', 'Dispel',
      ];
      const validTargets = ['Self', 'Source', 'Target', 'AuraTargets'];
      const validDamage = ['Physical', 'Magical', 'Pure', 'HpRemoval'];

      const trigger = String(item['trigger'] ?? item['Trigger'] ?? 'OnIntervalThink');
      const actionType = String(item['actionType'] ?? item['ActionType'] ?? 'DealDamage');
      const target = String(item['targetSelector'] ?? item['TargetSelector'] ?? 'Target');

      const result: Record<string, unknown> = {
        id: String(item['id'] ?? item['Id'] ?? `ai-action-${index}`),
        trigger: validTriggers.includes(trigger) ? trigger : 'OnIntervalThink',
        actionType: validActions.includes(actionType) ? actionType : 'DealDamage',
        targetSelector: validTargets.includes(target) ? target : 'Target',
        value: this.toNumber(item['value'] ?? item['Value'], 10),
        scaleByStacks: this.toBool(item['scaleByStacks'] ?? item['ScaleByStacks'], false),
      };

      if (item['damageType'] ?? item['DamageType']) {
        const dt = String(item['damageType'] ?? item['DamageType']);
        if (validDamage.includes(dt)) result['damageType'] = dt;
      }

      if (item['attributeType'] ?? item['AttributeType']) {
        result['attributeType'] = String(item['attributeType'] ?? item['AttributeType']);
      }

      if (item['modifierTemplateId'] ?? item['ModifierTemplateId']) {
        result['modifierTemplateId'] = String(item['modifierTemplateId'] ?? item['ModifierTemplateId']);
      }

      if (item['dispelType'] ?? item['DispelType']) {
        const dispel = String(item['dispelType'] ?? item['DispelType']);
        if (dispel === 'Basic' || dispel === 'Strong') result['dispelType'] = dispel;
      }

      if (item['eventName'] ?? item['EventName']) {
        result['eventName'] = String(item['eventName'] ?? item['EventName']);
      }

      return result;
    }) as unknown as BuffPayload['effectActions'];
  }

  private buildSystemPrompt(): string {
    return `你是一个游戏 Buff 系统配置助手。根据用户的自然语言描述，生成符合指定数据结构的 Buff 配置 JSON。只输出 JSON，不要包含任何解释、注释或 markdown 标记。

## 数据结构

{
  "key": "string（英文驼峰，如 PoisonDoT）",
  "displayName": "string（中文）",
  "description": "string（中文说明）",
  "modifierKind": "Buff" | "Debuff" | "Neutral",
  "duration": number（秒，-1=永久/被动）,
  "stackPolicy": "Refresh" | "Stack" | "Replace" | "Independent",
  "maxStacks": number（最小1）,
  "thinkInterval": number（秒，-1 = 无周期触发）,
  "statusEffects": string[]（从 ["Stun","Hex","Taunt","Fear","Silence","Muted","Disarm","Blind","Root","Leash","Slow","Break","Invulnerable","DebuffImmune","Untargetable","Ethereal"] 中选择）,
  "dispelRule": "NotDispellable" | "BasicDispellable" | "StrongDispellable",
  "affectedByStatusResistance": boolean,
  "removeOnDeath": boolean,
  "isHidden": boolean,
  "isPassive": boolean,
  "isAura": boolean,
  "auraRadius": number,
  "lingerDuration": number,
  "applyChance": number（0~1，默认1）,
  "applyCooldown": number（冷却秒数）,
  "requiredStatusEffects": string[],
  "tags": string[],
  "attributeModifiers": [
    {
      "id": "string",
      "attributeType": "string（从 ["Strength","Agility","Intelligence","MaxHp","HpRegen","MaxMana","ManaRegen","AttackDamage","AttackSpeed","BaseAttackTime","AttackRange","Armor","MagicResistance","StatusResistance","Evasion","MoveSpeed","CastRange","CooldownReduction","SpellAmplification","CritChance","CritDamage"] 中选择）",
      "op": "Add" | "PercentAdd" | "PercentMultiply" | "Override" | "Min" | "Max",
      "value": number,
      "scaleByStacks": boolean,
      "priority": number
    }
  ],
  "effectActions": [
    {
      "id": "string",
      "trigger": "OnCreated" | "OnRefresh" | "OnStackChanged" | "OnIntervalThink" | "OnAttackLanded" | "OnTakeDamage" | "OnDealDamage" | "OnDestroy" | "OnCustomEvent",
      "actionType": "DealDamage" | "Heal" | "ModifyAttribute" | "ApplyModifier" | "RemoveModifier" | "RefreshModifier" | "Dispel",
      "targetSelector": "Self" | "Source" | "Target" | "AuraTargets",
      "value": number,
      "scaleByStacks": boolean,
      "damageType": "Physical" | "Magical" | "Pure" | "HpRemoval"（仅 DealDamage 时）,
      "attributeType": "string"（仅 ModifyAttribute 时）,
      "modifierTemplateId": "string"（仅 ApplyModifier/RemoveModifier/RefreshModifier 时）,
      "dispelType": "Basic" | "Strong"（仅 Dispel 时）,
      "eventName": "string"（仅 OnCustomEvent 时）
    }
  ]
}

## 规则
1. 只输出 JSON 对象，不要有任何前缀或后缀文字。
2. key 必须是英文驼峰格式并以字母开头。
3. displayName 和 description 使用简洁中文。
4. 根据描述合理选择 modifierKind、stackPolicy、duration 等字段。
5. 如果描述提到具体数值，直接使用。如果没有提到，使用合理的默认值。
6. effectActions 的 trigger 要与描述中的触发时机匹配（如"每秒造成伤害"-> 周期触发 OnIntervalThink，"创建时造成伤害"-> OnCreated）。
7. attributeModifiers 用于描述持续性的属性修改，effectActions 的 ModifyAttribute 用于瞬时修改。
8. **重要**：光环效果（isAura=true）的 effectActions 必须使用 targetSelector: "AuraTargets"，让效果作用于光环范围内的单位，而不是光环持有者自身。
9. **重要**：降低属性用 op: "Add" 配合负整数（如护甲 -12 表示降低 12 点护甲）。
10. **重要**：百分比类属性（MagicResistance / StatusResistance / Evasion / CritChance / CritDamage / SpellAmplification / CooldownReduction）在系统中是小数存储（0.15=15%），降低时用 op: "Add" 配合负小数（如降低 15% 魔抗 -> op: "Add", value: -0.15）。不要用 PercentAdd 修改这些属性。
11. **重要**：整数类属性（Strength / Agility / Intelligence / MaxHp / HpRegen / MaxMana / ManaRegen / AttackDamage / AttackSpeed / Armor / MoveSpeed / AttackRange）降低时直接用 op: "Add" + 负整数（如降低 12 点护甲 -> op: "Add", value: -12）。
12. **重要**：如果描述中提到"每秒"、"每X秒"触发，thinkInterval 要设为对应值，effectActions 的 trigger 用 "OnIntervalThink"。
13. **重要**：如果描述中光环是"对敌军"、"对友军"作用，isAura 设为 true，effectActions 用 targetSelector: "AuraTargets"；如果是"自身光环"影响自己，用 targetSelector: "Self"。`;
  }
}
