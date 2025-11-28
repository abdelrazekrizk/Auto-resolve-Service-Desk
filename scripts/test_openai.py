"""
Test Azure OpenAI Integration
Uses official Azure OpenAI Python SDK pattern
"""

import json
from openai import AzureOpenAI

def test_azure_openai():
    """Test Azure OpenAI with official SDK pattern"""
    
    print("🤖 Testing Azure OpenAI Integration")
    print("=" * 50)
    
    # Load your configuration
    with open("demo-data/azure-config.json", 'r') as f:
        config = json.load(f)
    
    openai_config = config["azure_services"]["openai"]
    
    print(f"🔗 Endpoint: {openai_config['endpoint']}")
    print(f"🚀 Model: {openai_config['model']}")
    print(f"📦 Deployment: {openai_config['deployment_name']}")
    print(f"📅 API Version: {openai_config['api_version']}")
    print(f"🔑 API Key: {openai_config['api_key'][:8]}...")
    print()
    
    try:
        # Initialize client using official Azure OpenAI SDK pattern
        client = AzureOpenAI(
            api_version=openai_config["api_version"],
            azure_endpoint=openai_config["endpoint"],
            api_key=openai_config["api_key"]
        )
        
        # Test with a simple IT support ticket classification
        test_prompt = """
Classify this IT support ticket:

Title: Cannot access development environment
Description: Getting 403 error when trying to deploy to staging environment

Respond with JSON:
{
    "agent": "Automation",
    "priority": "High", 
    "confidence": 0.95,
    "reasoning": "Deployment access issue requires automation agent"
}
"""
        
        print("🧪 Testing ticket classification...")
        
        # Use official SDK pattern
        response = client.chat.completions.create(
            messages=[
                {"role": "system", "content": "You are an IT support ticket classifier. Respond only with valid JSON."},
                {"role": "user", "content": test_prompt}
            ],
            max_completion_tokens=openai_config["max_tokens"],
            temperature=openai_config["temperature"],
            top_p=1.0,
            frequency_penalty=0.0,
            presence_penalty=0.0,
            model=openai_config["deployment_name"]
        )
        
        result = response.choices[0].message.content
        
        print("✅ Azure OpenAI Response:")
        print("-" * 30)
        print(result)
        print("-" * 30)
        
        # Try to parse as JSON
        try:
            parsed = json.loads(result)
            print("✅ Valid JSON response received!")
            print(f"   Agent: {parsed.get('agent', 'Unknown')}")
            print(f"   Priority: {parsed.get('priority', 'Unknown')}")
            print(f"   Confidence: {parsed.get('confidence', 'Unknown')}")
            print(f"   Reasoning: {parsed.get('reasoning', 'N/A')}")
        except json.JSONDecodeError:
            print("⚠️  Response is not valid JSON, but OpenAI is working")
        
        print(f"\n🎯 Token Usage:")
        print(f"   Prompt: {response.usage.prompt_tokens}")
        print(f"   Completion: {response.usage.completion_tokens}")
        print(f"   Total: {response.usage.total_tokens}")
        
        # Test streaming (optional)
        print(f"\n🌊 Testing streaming response...")
        
        stream_response = client.chat.completions.create(
            stream=True,
            messages=[
                {"role": "system", "content": "You are a helpful assistant."},
                {"role": "user", "content": "Explain what Azure OpenAI is in one sentence."}
            ],
            max_completion_tokens=100,
            temperature=0.3,
            model=openai_config["deployment_name"]
        )
        
        print("Stream output: ", end="")
        for update in stream_response:
            if update.choices:
                print(update.choices[0].delta.content or "", end="")
        print()
        
        print("\n🏆 Azure OpenAI Integration Successful!")
        print("✅ Ready for demo ticket classification")
        print("✅ Streaming responses working")
        
        return True
        
    except Exception as e:
        print(f"❌ Azure OpenAI test failed: {e}")
        
        # Provide troubleshooting tips
        print("\n🔧 Troubleshooting:")
        print("1. Check API key is correct")
        print("2. Verify endpoint URL")
        print("3. Confirm model deployment name")
        print("4. Check Azure OpenAI resource is active")
        print("5. Verify API version is supported")
        
        return False

if __name__ == "__main__":
    success = test_azure_openai()