from fastapi import FastAPI
from pydantic import BaseModel
import uvicorn
import time
from datetime import datetime

app = FastAPI(title="Knowledge Agent", version="1.0.0")

# Mock knowledge base
KNOWLEDGE_BASE = {
    "login_error": "Reset password via admin portal or contact IT support",
    "network_issue": "Check VPN connection and firewall settings",
    "software_bug": "Update to latest version or contact support team",
    "performance_slow": "Clear browser cache and restart application",
    "access_denied": "Request permissions from system administrator",
    "system_crash": "Restart service and check system logs",
    "database_error": "Check database connection and run diagnostics",
    "email_not_working": "Verify email settings and server status"
}

class SearchRequest(BaseModel):
    query: str
    category: str

@app.get("/health")
async def health():
    return {"status": "healthy", "agent": "knowledge", "timestamp": datetime.now().isoformat()}

@app.post("/api/v1/search")
async def search_knowledge(request: SearchRequest):
    start_time = time.time()
    
    # Simple keyword matching
    results = []
    query_words = request.query.lower().split()
    
    for key, solution in KNOWLEDGE_BASE.items():
        # Check if any query word matches the knowledge base key
        if any(word in key for word in query_words):
            results.append({
                "title": key.replace("_", " ").title(),
                "solution": solution,
                "confidence": 0.9,
                "source": "internal_kb",
                "category": request.category
            })
    
    # If no direct matches, try partial matching
    if not results:
        for key, solution in KNOWLEDGE_BASE.items():
            if any(word in solution.lower() for word in query_words):
                results.append({
                    "title": key.replace("_", " ").title(),
                    "solution": solution,
                    "confidence": 0.7,
                    "source": "internal_kb",
                    "category": request.category
                })
    
    search_time = round(time.time() - start_time, 2)
    
    return {
        "results": results[:3],
        "total_found": len(results),
        "search_time": f"{search_time}s",
        "timestamp": datetime.now().isoformat()
    }

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8002)