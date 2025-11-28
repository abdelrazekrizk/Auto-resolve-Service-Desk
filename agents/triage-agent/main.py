from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
import uvicorn
import time
from datetime import datetime

app = FastAPI(title="Triage Agent", version="1.0.0")

class TicketRequest(BaseModel):
    title: str
    description: str
    user_id: str

@app.get("/health")
async def health():
    return {"status": "healthy", "agent": "triage", "timestamp": datetime.now().isoformat()}

@app.post("/api/v1/classify")
async def classify_ticket(ticket: TicketRequest):
    start_time = time.time()
    
    # Simple rule-based classification
    description_lower = ticket.description.lower()
    title_lower = ticket.title.lower()
    
    # Category classification
    if any(word in description_lower for word in ["error", "bug", "crash", "fail"]):
        category = "technical"
    elif any(word in description_lower for word in ["password", "login", "access", "permission"]):
        category = "security"
    elif any(word in description_lower for word in ["slow", "performance", "timeout"]):
        category = "performance"
    else:
        category = "general"
    
    # Priority classification
    if any(word in description_lower for word in ["urgent", "critical", "down", "outage"]):
        priority = "high"
    elif any(word in description_lower for word in ["important", "asap", "soon"]):
        priority = "medium"
    else:
        priority = "low"
    
    processing_time = round(time.time() - start_time, 2)
    
    return {
        "ticket_id": f"TKT-{hash(ticket.title) % 10000}",
        "category": category,
        "priority": priority,
        "confidence": 0.85,
        "processing_time": f"{processing_time}s",
        "timestamp": datetime.now().isoformat()
    }

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8001)