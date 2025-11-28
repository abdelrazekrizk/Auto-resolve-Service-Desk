import { Controller, Get, Query } from '@nestjs/common';

@Controller('api/v1')
export class AnalyticsController {
  private mockData = {
    totalTickets: 1247,
    resolvedTickets: 1089,
    avgResolutionTime: 32,
    satisfactionScore: 4.2,
    agentPerformance: {
      triage: { processed: 1247, accuracy: 94.5 },
      knowledge: { searches: 892, hitRate: 78.3 },
      escalation: { escalated: 158, responseTime: 12.5 },
      learning: { feedbackProcessed: 456, improvements: 23 }
    }
  };

  @Get('health')
  getHealth() {
    return { 
      status: 'healthy', 
      agent: 'analytics',
      timestamp: new Date().toISOString()
    };
  }

  @Get('dashboard')
  getDashboardData() {
    return {
      metrics: {
        totalTickets: this.mockData.totalTickets,
        resolvedTickets: this.mockData.resolvedTickets,
        resolutionRate: Math.round((this.mockData.resolvedTickets / this.mockData.totalTickets) * 100),
        avgResolutionTime: `${this.mockData.avgResolutionTime}s`,
        satisfactionScore: this.mockData.satisfactionScore
      },
      agentStatus: {
        triage: 'healthy',
        knowledge: 'healthy',
        escalation: 'healthy',
        learning: 'healthy',
        automation: 'healthy'
      },
      recentActivity: [
        { time: '2 min ago', event: 'Ticket TKT-1234 resolved automatically' },
        { time: '5 min ago', event: 'High priority ticket escalated' },
        { time: '8 min ago', event: 'Knowledge base updated with new solution' }
      ],
      timestamp: new Date().toISOString()
    };
  }

  @Get('performance')
  getPerformanceMetrics(@Query('agent') agent?: string) {
    if (agent && this.mockData.agentPerformance[agent]) {
      return {
        agent: agent,
        metrics: this.mockData.agentPerformance[agent],
        timestamp: new Date().toISOString()
      };
    }

    return {
      allAgents: this.mockData.agentPerformance,
      summary: {
        totalProcessed: this.mockData.totalTickets,
        overallAccuracy: 87.2,
        systemUptime: '99.95%'
      },
      timestamp: new Date().toISOString()
    };
  }
}