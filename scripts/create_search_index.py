"""
Create Azure AI Search Index - Python Version
Creates knowledge-base index and uploads sample data
"""

import json
import asyncio
import sys
from azure.search.documents.indexes.aio import SearchIndexClient
from azure.search.documents.aio import SearchClient
from azure.search.documents.indexes.models import SearchIndex
from azure.core.credentials import AzureKeyCredential

async def create_knowledge_base_index(search_endpoint: str, admin_key: str):
    """Create knowledge-base index with sample data"""
    
    print("🔍 Creating Azure AI Search Index: knowledge-base")
    print("=" * 50)
    
    credential = AzureKeyCredential(admin_key)
    
    try:
        # Initialize clients
        index_client = SearchIndexClient(endpoint=search_endpoint, credential=credential)
        
        # Load index schema
        with open("demo-data/knowledge-base-index.json", 'r') as f:
            index_definition = json.load(f)
        
        # Create index
        print("📋 Creating index schema...")
        index = SearchIndex.from_dict(index_definition)
        await index_client.create_index(index)
        print("✅ Index 'knowledge-base' created successfully!")
        
        # Wait for index to be ready
        await asyncio.sleep(3)
        
        # Upload sample data
        print("📊 Uploading sample knowledge base data...")
        
        search_client = SearchClient(
            endpoint=search_endpoint,
            index_name="knowledge-base", 
            credential=credential
        )
        
        # Load sample data
        with open("demo-data/knowledge-base-data.json", 'r') as f:
            sample_data = json.load(f)
        
        # Upload documents
        result = await search_client.upload_documents(documents=sample_data["value"])
        print(f"✅ Sample data uploaded successfully!")
        print(f"📈 Documents indexed: {len(sample_data['value'])}")
        
        # Test search
        print("🔍 Testing search functionality...")
        
        search_results = await search_client.search(
            search_text="ssl certificate",
            top=3,
            include_total_count=True
        )
        
        results_list = []
        async for result in search_results:
            results_list.append(result)
        
        print("✅ Search test successful!")
        print(f"🎯 Found {len(results_list)} results for 'ssl certificate'")
        
        for result in results_list:
            score = result.get('@search.score', 0)
            title = result.get('title', 'Unknown')
            print(f"   • {title} (Score: {score:.2f})")
        
        print("\n🏆 Knowledge Base Index Setup Complete!")
        print("🔧 Index Name: knowledge-base")
        print("📊 Documents: 10 sample knowledge articles")
        print("🎯 Ready for demo integration!")
        
        return True
        
    except Exception as e:
        print(f"❌ Error creating index: {e}")
        return False
    
    finally:
        await index_client.close()
        if 'search_client' in locals():
            await search_client.close()

def main():
    """Main function to create search index"""
    
    if len(sys.argv) != 3:
        print("Usage: python create_search_index.py <search_service_name> <admin_key>")
        print("Example: python create_search_index.py my-search-service abcd1234...")
        return
    
    search_service_name = sys.argv[1]
    admin_key = sys.argv[2]
    
    search_endpoint = f"https://{search_service_name}.search.windows.net"
    
    # Run async function
    success = asyncio.run(create_knowledge_base_index(search_endpoint, admin_key))
    
    if success:
        print("\n📋 Next Steps:")
        print("1. Update azure-config.json with your search service endpoint")
        print("2. Run: python demo_orchestrator.py")
        print("3. Test Knowledge Agent search functionality")
    else:
        print("\n❌ Index creation failed. Please check your credentials and try again.")

if __name__ == "__main__":
    main()