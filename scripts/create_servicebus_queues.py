"""
Create Azure Service Bus Queues and Topics
Sets up all required messaging infrastructure for agent coordination
"""

import asyncio
import sys
from azure.servicebus.management.aio import ServiceBusAdministrationClient
from azure.servicebus.management import QueueProperties, TopicProperties

async def create_servicebus_infrastructure(connection_string: str):
    """Create all required Service Bus queues and topics"""
    
    print("🚌 Creating Azure Service Bus Infrastructure")
    print("=" * 50)
    print(f"📍 Endpoint: https://sb-auto-resolve-dev.servicebus.windows.net")
    
    admin_client = ServiceBusAdministrationClient.from_connection_string(connection_string)
    
    try:
        # Define queues for each agent
        queues = [
            "triage-queue",
            "knowledge-queue", 
            "automation-queue",
            "escalation-queue",
            "learning-queue",
            "analytics-queue"
        ]
        
        # Define topics for coordination
        topics = [
            "ticket-events",
            "agent-coordination", 
            "performance-metrics"
        ]
        
        print("📋 Creating Agent Queues...")
        
        # Create queues
        for queue_name in queues:
            try:
                # Check if queue exists
                queue_exists = await admin_client.get_queue(queue_name)
                print(f"   ✅ Queue '{queue_name}' already exists")
            except:
                # Create queue if it doesn't exist
                queue_properties = QueueProperties(
                    name=queue_name,
                    max_delivery_count=10,
                    lock_duration="PT5M",  # 5 minutes
                    default_message_time_to_live="P14D"  # 14 days
                )
                await admin_client.create_queue(queue_properties)
                print(f"   ✅ Created queue: {queue_name}")
        
        print("\n📢 Creating Coordination Topics...")
        
        # Create topics
        for topic_name in topics:
            try:
                # Check if topic exists
                topic_exists = await admin_client.get_topic(topic_name)
                print(f"   ✅ Topic '{topic_name}' already exists")
            except:
                # Create topic if it doesn't exist
                topic_properties = TopicProperties(
                    name=topic_name,
                    default_message_time_to_live="P14D",  # 14 days
                    max_size_in_megabytes=1024
                )
                await admin_client.create_topic(topic_properties)
                print(f"   ✅ Created topic: {topic_name}")
        
        # Create subscriptions for topics
        print("\n📨 Creating Topic Subscriptions...")
        
        subscriptions = {
            "ticket-events": ["all-agents", "analytics-only"],
            "agent-coordination": ["coordination-hub"],
            "performance-metrics": ["monitoring-dashboard"]
        }
        
        for topic_name, subs in subscriptions.items():
            for sub_name in subs:
                try:
                    await admin_client.get_subscription(topic_name, sub_name)
                    print(f"   ✅ Subscription '{sub_name}' on '{topic_name}' already exists")
                except:
                    await admin_client.create_subscription(topic_name, sub_name)
                    print(f"   ✅ Created subscription: {sub_name} on {topic_name}")
        
        print("\n🏆 Service Bus Infrastructure Setup Complete!")
        print("🔧 Queues: 6 agent queues created")
        print("📢 Topics: 3 coordination topics created") 
        print("📨 Subscriptions: 4 topic subscriptions created")
        print("🎯 Ready for agent coordination!")
        
        return True
        
    except Exception as e:
        print(f"❌ Error setting up Service Bus: {e}")
        return False
    
    finally:
        await admin_client.close()

def main():
    """Main function to create Service Bus infrastructure"""
    
    if len(sys.argv) != 2:
        print("Usage: python create_servicebus_queues.py <connection_string>")
        print("Example: python create_servicebus_queues.py 'Endpoint=sb://...'")
        print("\nGet your connection string from Azure Portal:")
        print("Service Bus Namespace → Shared access policies → RootManageSharedAccessKey")
        return
    
    connection_string = sys.argv[1]
    
    # Validate connection string format
    if not connection_string.startswith("Endpoint=sb://"):
        print("❌ Invalid connection string format")
        print("Expected format: Endpoint=sb://namespace.servicebus.windows.net/;SharedAccessKeyName=...;SharedAccessKey=...")
        return
    
    # Run async function
    success = asyncio.run(create_servicebus_infrastructure(connection_string))
    
    if success:
        print("\n📋 Next Steps:")
        print("1. Update azure-config.json with your connection string")
        print("2. Store connection string in Key Vault as 'servicebus-connection-string'")
        print("3. Run: python demo_orchestrator.py")
        print("4. Test agent coordination via Service Bus")
    else:
        print("\n❌ Setup failed. Please check your connection string and permissions.")

if __name__ == "__main__":
    main()