import { IsIn, IsNumber, Min } from 'class-validator';
import { primaryAttributeTypes } from '../../common/buff.types';

export class UpdateAttributeFormulaDto {
  @IsIn(primaryAttributeTypes)
  primaryAttributeDefault: (typeof primaryAttributeTypes)[number];

  @IsNumber()
  strengthToMaxHp: number;

  @IsNumber()
  strengthToHpRegen: number;

  @IsNumber()
  agilityToArmor: number;

  @IsNumber()
  agilityToAttackSpeed: number;

  @IsNumber()
  intelligenceToMaxMana: number;

  @IsNumber()
  intelligenceToManaRegen: number;

  @IsNumber()
  intelligenceToMagicResistance: number;

  @IsNumber()
  primaryAttributeToAttackDamage: number;

  @IsNumber()
  universalAttributeToAttackDamage: number;

  @IsNumber()
  @Min(0)
  minAttackSpeed: number;

  @IsNumber()
  @Min(0)
  maxAttackSpeed: number;

  @IsNumber()
  @Min(0)
  physicalArmorFactor: number;
}
