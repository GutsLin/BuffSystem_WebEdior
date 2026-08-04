import { Type } from 'class-transformer';
import {
  IsArray,
  IsBoolean,
  IsIn,
  IsInt,
  IsNotEmpty,
  IsNumber,
  IsOptional,
  IsString,
  Matches,
  Min,
  ValidateNested,
} from 'class-validator';
import {
  attributeOps,
  damageTypes,
  dispelRules,
  effectActionTypes,
  effectTriggers,
  modifierKinds,
  stackPolicies,
  targetSelectors,
} from '../../common/buff.types';

class AttributeModifierDto {
  @IsString()
  id: string;

  @IsString()
  @IsNotEmpty()
  attributeType: string;

  @IsIn(attributeOps)
  op: (typeof attributeOps)[number];

  @IsNumber()
  value: number;

  @IsBoolean()
  scaleByStacks: boolean;

  @IsInt()
  priority: number;
}

class EffectConditionDto {
  @IsOptional()
  @IsNumber()
  healthPercentMin?: number;

  @IsOptional()
  @IsNumber()
  healthPercentMax?: number;

  @IsOptional()
  @IsString()
  requiredStatusEffect?: string;
}

class EffectActionDto {
  @IsString()
  id: string;

  @IsIn(effectTriggers)
  trigger: (typeof effectTriggers)[number];

  @IsIn(effectActionTypes)
  actionType: (typeof effectActionTypes)[number];

  @IsIn(targetSelectors)
  targetSelector: (typeof targetSelectors)[number];

  @IsNumber()
  value: number;

  @IsBoolean()
  scaleByStacks: boolean;

  @IsOptional()
  @IsIn(damageTypes)
  damageType?: (typeof damageTypes)[number];

  @IsOptional()
  @IsString()
  attributeType?: string;

  @IsOptional()
  @IsString()
  modifierTemplateId?: string;

  @IsOptional()
  @IsIn(['Basic', 'Strong'])
  dispelType?: 'Basic' | 'Strong';

  @IsOptional()
  @IsString()
  eventName?: string;

  @IsOptional()
  @ValidateNested()
  @Type(() => EffectConditionDto)
  condition?: EffectConditionDto;
}

export class UpsertBuffDto {
  @IsString()
  @IsNotEmpty()
  @Matches(/^[A-Za-z][A-Za-z0-9_]*$/, {
    message: '效果标识必须以字母开头，并且只能包含字母、数字或下划线',
  })
  key: string;

  @IsString()
  @IsNotEmpty()
  displayName: string;

  @IsString()
  description: string;

  @IsIn(modifierKinds)
  modifierKind: (typeof modifierKinds)[number];

  @IsNumber()
  duration: number;

  @IsIn(stackPolicies)
  stackPolicy: (typeof stackPolicies)[number];

  @IsInt()
  @Min(1)
  maxStacks: number;

  @IsNumber()
  thinkInterval: number;

  @IsArray()
  @IsString({ each: true })
  statusEffects: string[];

  @IsIn(dispelRules)
  dispelRule: (typeof dispelRules)[number];

  @IsBoolean()
  affectedByStatusResistance: boolean;

  @IsBoolean()
  removeOnDeath: boolean;

  @IsBoolean()
  isHidden: boolean;

  @IsBoolean()
  isPassive: boolean;

  @IsBoolean()
  isAura: boolean;

  @IsNumber()
  @Min(0)
  auraRadius: number;

  @IsNumber()
  @Min(0)
  lingerDuration: number;

  @IsArray()
  @ValidateNested({ each: true })
  @Type(() => AttributeModifierDto)
  attributeModifiers: AttributeModifierDto[];

  @IsArray()
  @ValidateNested({ each: true })
  @Type(() => EffectActionDto)
  effectActions: EffectActionDto[];

  @IsArray()
  @IsString({ each: true })
  tags: string[];

  @IsNumber()
  applyChance: number;

  @IsNumber()
  applyCooldown: number;

  @IsArray()
  @IsString({ each: true })
  requiredStatusEffects: string[];
}
