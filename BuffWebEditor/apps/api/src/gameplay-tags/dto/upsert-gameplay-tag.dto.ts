import { IsBoolean, IsIn, IsInt, IsNotEmpty, IsOptional, IsString, Matches, Min } from 'class-validator';
import { gameplayTagSources } from '../../common/gameplay-tags.types';

export class UpsertGameplayTagDto {
  @IsString()
  @IsNotEmpty()
  @Matches(/^[A-Za-z][A-Za-z0-9]*(?:\.[A-Za-z][A-Za-z0-9]*)*$/, {
    message: '标签名称只能使用字母、数字和点号，并且每级必须以字母开头',
  })
  name: string;

  @IsString()
  @IsNotEmpty()
  displayName: string;

  @IsString()
  description: string;

  @IsInt()
  @Min(0)
  flags: number;

  @IsIn(gameplayTagSources)
  @IsOptional()
  source?: (typeof gameplayTagSources)[number];

  @IsBoolean()
  @IsOptional()
  deprecated?: boolean;
}
