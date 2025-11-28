"""
Triage Agent - Azure OpenAI Integration
Uses official Azure OpenAI Python SDK patterns
"""

import json
import asyncio
from typing import Dict, List, Optional
from openai import AzureOpenAI
import logging
import time

class TriageAzureIntegration:
    def __init__(self, config_path: str = "../../demo-data/azure-config.json"):
        self.config = self._load_config(config_path)
        self.openai_client = None
        self.logger = logging.getLogger(__name__)
        
    def _load_config(self, config_path: str) -> Dict:
        """Load Azure configuration"""
        with open(config_path, 'r') as f:
            return json.load(f)
    
    def initialize(self):
        """Initialize Azure OpenAI client using official SDK pattern"""
        try:
            # Use official Azure OpenAI SDK pattern
            self.openai_client = AzureOpenAI(
                api_version=self.config["azure_services"]["openai"]["api_version"],
                azure_endpoint=self.config["azure_services"]["openai"]["endpoint"],
                api_key=self.config["azure_services"]["openai"]["api_key"]
            )
            
            self.logger.info("Azure OpenAI client initialized successfully")
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to initialize Azure OpenAI: {e}")
            return False
    
    def classify_ticket(self, ticket: Dict) -> Dict:
        """Classify ticket using Azure OpenAI with official SDK pattern"""
        try:
            start_time = time.time()
            prompt = self._build_classification_prompt(ticket)
            
            # Use official Azure OpenAI SDK pattern
            response = self.openai_client.chat.completions.create(
                messages=[
                    {"role": "system", "content": "You are an expert IT support ticket classifier. Respond only with valid JSON."},
                    {"role": "user", "content": prompt}
                ],
                max_completion_tokens=self.config["azure_services"]["openai"]["max_tokens"],
                temperature=self.config["azure_services"]["openai"]["temperature"],
                top_p=1.0,
                frequency_penalty=0.0,
                presence_penalty=0.0,
                model=self.config["azure_services"]["openai"]["deployment_name"]
            )
            
            processing_time = time.time() - start_time
            classification = self._parse_classification_response(response.choices[0].message.content)
            
            return {
                "ticket_id": ticket["id"],
                "classification": classification,
                "confidence": classification.get("confidence", 0.95),
                "assigned_agent": classification.get("agent", "Escalation"),
                "priority": classification.get("priority", ticket.get("priority", "Medium")),
                "estimated_resolution_time": classification.get("resolution_time", "120s"),
                "processing_time": f"{processing_time:.1f}s",
                "azure_service": f"Azure OpenAI {self.config['azure_services']['openai']['model']}",
                "token_usage": {
                    "prompt_tokens": response.usage.prompt_tokens,
                    "completion_tokens": response.usage.completion_tokens,
                    "total_tokens": response.usage.total_tokens
                }
            }
            
        except Exception as e:
            self.logger.error(f"Classification failed: {e}")
            return self._fallback_classification(ticket)
    
    def _build_classification_prompt(self, ticket: Dict) -> str:
        """Build classification prompt for OpenAI"""
        return f"""
Classify this IT support ticket and assign to the appropriate agent:

Title: {ticket['title']}
Description: {ticket['description']}
Current Priority: {ticket.get('priority', 'Medium')}

Available Agents:
- Knowledge: Information requests, documentation, how-to guides
- Automation: Technical issues, deployments, configurations, workflows
- Escalation: Critical issues, security, account problems, complex issues
- Learning: Model training, AI/ML issues, feedback processing
- Analytics: Reports, dashboards, data analysis, metrics

Respond ONLY with valid JSON in this exact format:
{{
    "agent": "agent_name",
    "category": "category_name", 
    "priority": "Low|Medium|High|Critical",
    "confidence": 0.95,
    "resolution_time": "30s",
    "reasoning": "brief explanation"
}}
"""
    
    def _parse_classification_response(self, response: str) -> Dict:
        """Parse OpenAI classification response"""
        try:
            # Extract JSON from response
            start = response.find('{')
            end = response.rfind('}') + 1
            if start >= 0 and end > start:
                json_str = response[start:end]
                return json.loads(json_str)
            else:
                raise ValueError("No JSON found in response")
        except Exception as e:
            self.logger.warning(f"Failed to parse OpenAI response: {e}")
            return {
                "agent": "Escalation",
                "category": "Unknown",
                "priority": "Medium",
                "confidence": 0.8,
                "resolution_time": "120s",
                "reasoning": "Fallback classification due to parsing error"
            }
    
    def _fallback_classification(self, ticket: Dict) -> Dict:
        """Fallback classification when Azure OpenAI fails"""
        # Simple rule-based fallback
        title_lower = ticket['title'].lower()
        desc_lower = ticket['description'].lower()
        
        if any(word in title_lower + desc_lower for word in ['how', 'guide', 'documentation', 'configure']):
            agent = "Knowledge"
            resolution_time = "30s"
        elif any(word in title_lower + desc_lower for word in ['deploy', 'automation', 'workflow', 'pipeline']):
            agent = "Automation" 
            resolution_time = "60s"
        elif any(word in title_lower + desc_lower for word in ['security', 'vulnerability', 'critical', 'urgent']):
            agent = "Escalation"
            resolution_time = "90s"
        elif any(word in title_lower + desc_lower for word in ['report', 'analytics', 'dashboard', 'metrics']):
            agent = "Analytics"
            resolution_time = "45s"
        else:
            agent = "Escalation"
            resolution_time = "120s"
            
        return {
            "ticket_id": ticket["id"],
            "classification": {
                "agent": agent,
                "category": ticket.get("category", "General"),
                "priority": ticket.get("priority", "Medium"),
                "confidence": 0.85,
                "resolution_time": resolution_time,
                "reasoning": "Fallback rule-based classification"
            },
            "assigned_agent": agent,
            "processing_time": "1.2s",
            "azure_service": "Fallback (Local Rules)"
        }

# Demo usage
def demo_classification():
    """Demo ticket classification with Azure OpenAI"""
    triage = TriageAzureIntegration()
    
    if triage.initialize():
        # Load demo tickets
        with open("../../demo-data/mock-tickets.json", 'r') as f:
            data = json.load(f)
        
        print("🚀 Triage Agent - Azure OpenAI Classification Demo")
        print("=" * 50)
        print(f"🤖 Model: {triage.config['azure_services']['openai']['model']}")
        print(f"🚀 Deployment: {triage.config['azure_services']['openai']['deployment_name']}")
        print(f"🔗 Endpoint: {triage.config['azure_services']['openai']['endpoint']}")
        print(f"📅 API Version: {triage.config['azure_services']['openai']['api_version']}")
        print()
        
        for ticket in data["tickets"][:3]:  # Demo first 3 tickets
            print(f"📋 Processing: {ticket['title']}")
            result = triage.classify_ticket(ticket)
            
            print(f"   ✅ Assigned to: {result['assigned_agent']}")
            print(f"   ⏱️  Processing time: {result['processing_time']}")
            print(f"   🎯 Confidence: {result['confidence']:.1%}")
            print(f"   🔧 Azure Service: {result['azure_service']}")
            
            if 'token_usage' in result:
                print(f"   🎫 Tokens: {result['token_usage']['total_tokens']} total")
            
            print(f"   💡 Reasoning: {result['classification'].get('reasoning', 'N/A')}")
            print()

if __name__ == "__main__":
    demo_classification()