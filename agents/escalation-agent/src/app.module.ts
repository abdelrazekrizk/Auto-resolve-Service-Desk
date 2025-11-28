import { Module } from '@nestjs/common';
import { EscalationController } from './escalation.controller';

@Module({
  controllers: [EscalationController],
})
export class AppModule {}