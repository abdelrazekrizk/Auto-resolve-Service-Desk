import { Controller, Post, Body, Get } from '@nestjs/common';

interface EscalationRequest {
  ticketId: string;
  reason: string;
  urgency: 'low' | 'medium' | 'high' | 'critical';
}

@Controller('api/v1')
export class EscalationController {
  
  @Get('health')
  getHealth() {
    return { 
      status: 'healthy', 
      agent: 'escalation',
      timestamp: new Date().toISOString()
    };
  }

  @Post('escalate')
  async escalateTicket(@Body() request: EscalationRequest) {
    // Mock escalation logic
    const channels = this.getNotificationChannels(request.urgency);
    
    return {
      escalationId: `ESC-${Date.now()}`,
      ticketId: request.ticketId,
      channels: channels,
      notificationsSent: channels.length,
      estimatedResponse: this.getResponseTime(request.urgency),
      timestamp: new Date().toISOString()
    };
  }

  private getNotificationChannels(urgency: string): string[] {
    switch (urgency) {
      case 'critical':
        return ['email', 'sms', 'teams', 'phone'];
      case 'high':
        return ['email', 'teams'];
      case 'medium':
        return ['email'];
      default:
        return ['email'];
    }
  }

  private getResponseTime(urgency: string): string {
    switch (urgency) {
      case 'critical':
        return '5 minutes';
      case 'high':
        return '15 minutes';
      case 'medium':
        return '1 hour';
      default:
        return '4 hours';
    }
  }
}