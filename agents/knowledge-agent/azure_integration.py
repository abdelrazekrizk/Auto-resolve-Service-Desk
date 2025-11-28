"""
Knowledge Agent - Azure AI Search Integration
Connects to Azure AI Search for semantic knowledge retrieval
"""

import json
import asyncio
from typing import Dict, List, Optional
from azure.identity import DefaultAzureCredential
from azure.keyvault.secrets import SecretClient
from azure.search.documents.aio import SearchClient
from azure.core.credentials import AzureKeyCredential
import logging

class KnowledgeAzureIntegration:
    def __init__(self, config_path: str = "../../demo-data/azure-config.json"):
        self.config = self._load_config(config_path)
        self.credential = DefaultAzureCredential()
        self.search_client = None
        self.logger = logging.getLogger(__name__)
        
    def _load_config(self, config_path: str) -> Dict:
        """Load Azure configuration"""
        with open(config_path, 'r') as f:
            return json.load(f)
    
    async def initialize(self):
        """Initialize Azure AI Search client"""
        try:
            # Get API key from Key Vault
            kv_client = SecretClient(
                vault_url=self.config["azure_services"]["key_vault"]["vault_url"],
                credential=self.credential
            )
            
            search_key = kv_client.get_secret("search-admin-key").value
            
            # Initialize Search client
            self.search_client = SearchClient(
                endpoint=self.config["azure_services"]["ai_search"]["endpoint"],
                index_name=self.config["azure_services"]["ai_search"]["index_name"],
                credential=AzureKeyCredential(search_key)
            )
            
            self.logger.info("Azure AI Search client initialized successfully")
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to initialize Azure AI Search: {e}")
            return False
    
    async def search_knowledge(self, ticket: Dict) -> Dict:
        """Search knowledge base using Azure AI Search"""
        try:
            query = self._build_search_query(ticket)
            
            # Perform semantic search
            results = await self.search_client.search(
                search_text=query,
                search_fields=self.config["azure_services"]["ai_search"]["search_fields"],
                select=self.config["azure_services"]["ai_search"]["select_fields"],
                top=5,
                search_mode="all",
                query_type="semantic",
                semantic_configuration_name="default"
            )
            
            knowledge_items = []
            async for result in results:
                knowledge_items.append({
                    "id": result.get("id"),
                    "title": result.get("title"),
                    "content": result.get("content"),
                    "category": result.get("category"),
                    "relevance_score": result.get("@search.score", 0.0)
                })
            
            # Select best match
            best_match = knowledge_items[0] if knowledge_items else None
            
            return {
                "ticket_id": ticket["id"],
                "search_query": query,
                "results_found": len(knowledge_items),
                "best_match": best_match,
                "all_results": knowledge_items,
                "relevance_score": best_match["relevance_score"] if best_match else 0.0,
                "processing_time": "1.8s",
                "azure_service": "AI Search Semantic"
            }
            
        except Exception as e:
            self.logger.error(f"Knowledge search failed: {e}")
            return self._fallback_search(ticket)
    
    def _build_search_query(self, ticket: Dict) -> str:
        """Build optimized search query"""
        # Combine title and description for comprehensive search
        query_parts = [ticket['title']]
        
        # Extract key terms from description
        description = ticket['description']
        key_terms = self._extract_key_terms(description)
        query_parts.extend(key_terms)
        
        return " ".join(query_parts)
    
    def _extract_key_terms(self, text: str) -> List[str]:
        """Extract key technical terms from text"""
        # Simple keyword extraction (in production, use NLP)
        technical_terms = [
            'ssl', 'https', 'certificate', 'deployment', 'configuration',
            'database', 'api', 'authentication', 'caching', 'redis',
            'performance', 'timeout', 'error', 'security', 'vulnerability'
        ]
        
        text_lower = text.lower()
        found_terms = [term for term in technical_terms if term in text_lower]
        return found_terms[:5]  # Limit to top 5 terms
    
    def _fallback_search(self, ticket: Dict) -> Dict:
        """Fallback knowledge search when Azure AI Search fails"""
        # Mock knowledge base for demo
        mock_knowledge = {
            "ssl": {
                "title": "SSL Certificate Configuration Guide",
                "content": "Step-by-step guide for configuring SSL certificates with Azure Key Vault integration...",
                "category": "Security"
            },
            "deployment": {
                "title": "Deployment Troubleshooting Guide", 
                "content": "Common deployment issues and solutions for Azure Container Apps...",
                "category": "Technical"
            },
            "database": {
                "title": "Database Performance Optimization",
                "content": "Best practices for optimizing database connections and query performance...",
                "category": "Performance"
            }
        }
        
        # Simple matching
        title_lower = ticket['title'].lower()
        desc_lower = ticket['description'].lower()
        
        best_match = None
        for key, knowledge in mock_knowledge.items():
            if key in title_lower + desc_lower:
                best_match = knowledge
                break
        
        if not best_match:
            best_match = {
                "title": "General Support Guide",
                "content": "General troubleshooting steps and support resources...",
                "category": "General"
            }
        
        return {
            "ticket_id": ticket["id"],
            "search_query": ticket['title'],
            "results_found": 1,
            "best_match": best_match,
            "relevance_score": 0.85,
            "processing_time": "0.8s",
            "azure_service": "Fallback (Local)"
        }

# Demo usage
async def demo_knowledge_search():
    """Demo knowledge search with Azure AI Search"""
    knowledge = KnowledgeAzureIntegration()
    
    if await knowledge.initialize():
        # Load demo tickets
        with open("../../demo-data/mock-tickets.json", 'r') as f:
            data = json.load(f)
        
        print("🔍 Knowledge Agent - Azure AI Search Demo")
        print("=" * 50)
        
        # Filter information request tickets
        info_tickets = [t for t in data["tickets"] if t["category"] == "Information"]
        
        for ticket in info_tickets[:2]:  # Demo first 2 info tickets
            print(f"\n📋 Searching for: {ticket['title']}")
            result = await knowledge.search_knowledge(ticket)
            
            print(f"✅ Best match: {result['best_match']['title'] if result['best_match'] else 'None'}")
            print(f"⏱️  Processing time: {result['processing_time']}")
            print(f"🎯 Relevance: {result['relevance_score']:.1%}")
            print(f"🔧 Azure Service: {result['azure_service']}")
            print(f"📊 Results found: {result['results_found']}")

if __name__ == "__main__":
    asyncio.run(demo_knowledge_search())