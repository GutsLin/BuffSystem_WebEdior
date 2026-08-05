import { Container, Graphics } from 'pixi.js';
import { Hero } from '../engine/Hero';

export class SpriteGenerator {
  createHero(parent: Container): Hero {
    const container = new Container();
    container.zIndex = 100;
    parent.addChild(container);

    // Glow aura
    const aura = new Graphics();
    aura.circle(0, 0, 30).fill({ color: 0x6d5dfc, alpha: 0.1 });
    const a2 = new Graphics();
    a2.circle(0, 0, 22).fill({ color: 0x8b7dff, alpha: 0.15 });
    container.addChild(aura, a2);

    const hero = new Hero(container);
    return hero;
  }

  createProjectileTexture(): void {
    // Procedural, no external textures needed
  }

  createEnemyGraphic(type: string): Graphics {
    const g = new Graphics();
    switch (type) {
      case 'voidWalker':
        g.regularPoly(0, 0, 12, 6).fill({ color: 0x6b21a8 });
        break;
      case 'stoneBeast':
        g.rect(-22, -18, 44, 36).fill({ color: 0x92400e });
        g.rect(-12, -10, 24, 20).fill({ color: 0xb5651d });
        break;
      case 'elementMage':
        g.regularPoly(0, 0, 14, 4, Math.PI / 4).fill({ color: 0x2563eb });
        break;
      case 'paladin':
        g.moveTo(0, -26).lineTo(26, 13).lineTo(-26, 13).closePath().fill({ color: 0xca8a04 });
        break;
    }
    return g;
  }
}
