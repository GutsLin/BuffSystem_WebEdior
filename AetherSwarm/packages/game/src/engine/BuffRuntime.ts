import type { HeroType } from './Game';

interface BuffStats {
  moveSpeed: number;
  attackDamage: number;
  maxHp: number;
  hpRegen: number;
  armor: number;
  magicResistance: number;
}

const BASE_STATS: Record<HeroType, BuffStats> = {
  Strength:   { moveSpeed: 100, attackDamage: 20, maxHp: 150, hpRegen: 2, armor: 5, magicResistance: 0.15 },
  Agility:    { moveSpeed: 130, attackDamage: 18, maxHp: 100, hpRegen: 1, armor: 3, magicResistance: 0.10 },
  Intelligence:{ moveSpeed: 110, attackDamage: 25, maxHp: 90,  hpRegen: 0.5, armor: 2, magicResistance: 0.25 },
  Universal:  { moveSpeed: 115, attackDamage: 20, maxHp: 110, hpRegen: 1, armor: 3, magicResistance: 0.15 },
};

export class BuffRuntime {
  stats: BuffStats;

  constructor(heroType: HeroType) {
    this.stats = { ...BASE_STATS[heroType] };
  }

  applyBuff(): void {
    // Future: apply BuffTemplate from editor to modify stats
  }
}
