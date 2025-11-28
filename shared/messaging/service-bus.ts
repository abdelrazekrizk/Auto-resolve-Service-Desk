interface AgentMessage {
  id: string;
  from: string;
  to: string;
  type: string;
  payload: any;
  timestamp: Date;
}

export class MockServiceBus {
  private static messages: AgentMessage[] = [];
  
  static async sendMessage(message: AgentMessage): Promise<void> {
    this.messages.push(message);
    console.log(`Message sent: ${message.from} -> ${message.to} (${message.type})`);
  }
  
  static async getMessages(agentName: string): Promise<AgentMessage[]> {
    return this.messages.filter(m => m.to === agentName);
  }

  static async getAllMessages(): Promise<AgentMessage[]> {
    return [...this.messages];
  }

  static clearMessages(): void {
    this.messages = [];
  }
}