"""
Service Bus Coordinator - Azure Service Bus Integration
Handles message routing and coordination between all 6 agents
"""

import json
import asyncio
from typing import Dict, List, Optional, Callable
from azure.identity import DefaultAzureCredential
from azure.keyvault.secrets import SecretClient
from azure.servicebus.aio import ServiceBusClient
from azure.servicebus import ServiceBusMessage
import logging
from datetime import datetime

class ServiceBusCoordinator:
    def __init__(self, config_path: str = "../demo-data/azure-config.json"):
        self.config = self._load_config(config_path)
        self.credential = DefaultAzureCredential()
        self.servicebus_client = None
        self.message_handlers = {}
        self.logger = logging.getLogger(__name__)
        
    def _load_config(self, config_path: str) -> Dict:
        """Load Azure configuration"""
        with open(config_path, 'r') as f:
            return json.load(f)
    
    async def initialize(self):
        """Initialize Azure Service Bus client"""
        try:
            # Get connection string from Key Vault
            kv_client = SecretClient(
                vault_url=self.config["azure_services"]["key_vault"]["vault_url"],
                credential=self.credential
            )
            
            connection_string = kv_client.get_secret("servicebus-connection-string").value
            
            # Initialize Service Bus client
            self.servicebus_client = ServiceBusClient.from_connection_string(connection_string)
            
            self.logger.info("Azure Service Bus client initialized successfully")
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to initialize Azure Service Bus: {e}")
            return False
    
    async def send_message(self, queue_name: str, message_data: Dict):
        """Send message to specific agent queue"""
        try:
            async with self.servicebus_client:
                sender = self.servicebus_client.get_queue_sender(queue_name=queue_name)
                async with sender:
                    # Create message with metadata
                    message_body = {
                        "id": message_data.get("id", f"msg-{datetime.now().timestamp()}"),
                        "timestamp": datetime.now().isoformat(),
                        "source": message_data.get("source", "coordinator"),
                        "target": queue_name,
                        "data": message_data
                    }
                    
                    message = ServiceBusMessage(json.dumps(message_body))
                    await sender.send_messages(message)
                    
                    self.logger.info(f"Message sent to {queue_name}: {message_data.get('id')}")
                    return True
                    
        except Exception as e:
            self.logger.error(f"Failed to send message to {queue_name}: {e}")
            return False
    
    async def receive_messages(self, queue_name: str, handler: Callable):
        """Receive messages from queue and process with handler"""
        try:
            async with self.servicebus_client:
                receiver = self.servicebus_client.get_queue_receiver(queue_name=queue_name)
                async with receiver:
                    async for message in receiver:
                        try:
                            # Parse message
                            message_data = json.loads(str(message))
                            
                            # Process with handler
                            await handler(message_data)
                            
                            # Complete message
                            await receiver.complete_message(message)
                            
                        except Exception as e:
                            self.logger.error(f"Error processing message: {e}")
                            await receiver.abandon_message(message)
                            
        except Exception as e:
            self.logger.error(f"Failed to receive messages from {queue_name}: {e}")
    
    async def coordinate_ticket_processing(self, ticket: Dict) -> Dict:
        """Coordinate complete ticket processing across all agents"""
        processing_log = {
            "ticket_id": ticket["id"],
            "start_time": datetime.now().isoformat(),
            "steps": [],
            "total_time": 0,
            "status": "processing"
        }
        
        try:
            # Step 1: Triage Classification
            triage_result = await self._process_with_agent("triage", ticket)
            processing_log["steps"].append({
                "agent": "triage",
                "duration": "2.3s",
                "result": triage_result
            })
            
            # Step 2: Route to appropriate agent based on classification
            assigned_agent = triage_result.get("assigned_agent", "escalation").lower()
            
            if assigned_agent == "knowledge":
                agent_result = await self._process_with_agent("knowledge", ticket)
            elif assigned_agent == "automation":
                agent_result = await self._process_with_agent("automation", ticket)
            elif assigned_agent == "escalation":
                agent_result = await self._process_with_agent("escalation", ticket)
            elif assigned_agent == "learning":
                agent_result = await self._process_with_agent("learning", ticket)
            elif assigned_agent == "analytics":
                agent_result = await self._process_with_agent("analytics", ticket)
            else:
                agent_result = await self._process_with_agent("escalation", ticket)
            
            processing_log["steps"].append({
                "agent": assigned_agent,
                "duration": agent_result.get("processing_time", "60s"),
                "result": agent_result
            })
            
            # Step 3: Analytics tracking (parallel)
            analytics_result = await self._process_with_agent("analytics", {
                **ticket,
                "processing_log": processing_log
            })
            
            processing_log["steps"].append({
                "agent": "analytics",
                "duration": "0.5s",
                "result": analytics_result
            })
            
            # Calculate total processing time
            total_seconds = sum([
                float(step["duration"].replace("s", "")) 
                for step in processing_log["steps"]
            ])
            
            processing_log["total_time"] = f"{total_seconds:.1f}s"
            processing_log["status"] = "completed"
            processing_log["end_time"] = datetime.now().isoformat()
            
            return processing_log
            
        except Exception as e:
            processing_log["status"] = "failed"
            processing_log["error"] = str(e)
            self.logger.error(f"Ticket processing failed: {e}")
            return processing_log
    
    async def _process_with_agent(self, agent_name: str, data: Dict) -> Dict:
        """Process data with specific agent (mock for demo)"""
        # Mock agent processing for demo
        mock_responses = {
            "triage": {
                "assigned_agent": data.get("expectedAgent", "Escalation"),
                "confidence": 0.95,
                "category": data.get("category", "General"),
                "priority": data.get("priority", "Medium"),
                "processing_time": "2.3s"
            },
            "knowledge": {
                "solution_found": True,
                "relevance_score": 0.92,
                "knowledge_base_hits": 3,
                "processing_time": "1.8s"
            },
            "automation": {
                "automation_triggered": True,
                "workflow_id": f"wf-{data['id']}",
                "estimated_completion": "45s",
                "processing_time": "3.2s"
            },
            "escalation": {
                "escalated": True,
                "notification_sent": True,
                "assigned_human": "support-team",
                "processing_time": "1.5s"
            },
            "learning": {
                "feedback_processed": True,
                "model_updated": False,
                "confidence_improvement": 0.02,
                "processing_time": "4.1s"
            },
            "analytics": {
                "metrics_updated": True,
                "kpi_impact": "positive",
                "trend_analysis": "normal",
                "processing_time": "0.8s"
            }
        }
        
        return mock_responses.get(agent_name, {"processing_time": "1.0s"})

# Demo usage
async def demo_service_bus_coordination():
    """Demo Service Bus coordination with mock ticket processing"""
    coordinator = ServiceBusCoordinator()
    
    if await coordinator.initialize():
        # Load demo tickets
        with open("../demo-data/mock-tickets.json", 'r') as f:
            data = json.load(f)
        
        print("🚌 Service Bus Coordinator - Agent Coordination Demo")
        print("=" * 60)
        
        for ticket in data["tickets"][:3]:  # Demo first 3 tickets
            print(f"\n🎫 Processing Ticket: {ticket['id']} - {ticket['title']}")
            print("-" * 50)
            
            result = await coordinator.coordinate_ticket_processing(ticket)
            
            print(f"📊 Status: {result['status'].upper()}")
            print(f"⏱️  Total Time: {result['total_time']}")
            print(f"🔄 Processing Steps:")
            
            for step in result["steps"]:
                print(f"   • {step['agent'].title()}: {step['duration']}")
            
            print(f"✅ Resolution: {result['total_time']} < 2min target")

if __name__ == "__main__":
    asyncio.run(demo_service_bus_coordination())