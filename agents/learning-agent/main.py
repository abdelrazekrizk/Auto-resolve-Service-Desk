from fastapi import FastAPI
from pydantic import BaseModel
import uvicorn
from datetime import datetime
from typing import List

app = FastAPI(title="Learning Agent", version="1.0.0")

# Mock feedback storage
feedback_data = []
performance_metrics = {
    "total_tickets": 0,
    "successful_resolutions": 0,
    "average_satisfaction": 0.0
}

class FeedbackRequest(BaseModel):
    ticket_id: str
    user_satisfaction: int  # 1-5 scale
    resolution_successful: bool
    comments: str = ""

class PerformanceData(BaseModel):
    agent_name: str
    metric_type: str
    value: float

@app.get("/health")
async def health():
    return {"status": "healthy", "agent": "learning", "timestamp": datetime.now().isoformat()}

@app.post("/api/v1/feedback")
async def process_feedback(feedback: FeedbackRequest):
    # Store feedback
    feedback_entry = {
        "ticket_id": feedback.ticket_id,
        "satisfaction": feedback.user_satisfaction,
        "successful": feedback.resolution_successful,
        "comments": feedback.comments,
        "timestamp": datetime.now().isoformat()
    }
    feedback_data.append(feedback_entry)
    
    # Update metrics
    performance_metrics["total_tickets"] += 1
    if feedback.resolution_successful:
        performance_metrics["successful_resolutions"] += 1
    
    # Calculate average satisfaction
    total_satisfaction = sum(f["satisfaction"] for f in feedback_data)
    performance_metrics["average_satisfaction"] = round(total_satisfaction / len(feedback_data), 2)
    
    return {
        "feedback_id": f"FB-{len(feedback_data)}",
        "processed": True,
        "recommendations": generate_recommendations(),
        "timestamp": datetime.now().isoformat()
    }

@app.get("/api/v1/metrics")
async def get_performance_metrics():
    success_rate = 0
    if performance_metrics["total_tickets"] > 0:
        success_rate = round(
            (performance_metrics["successful_resolutions"] / performance_metrics["total_tickets"]) * 100, 2
        )
    
    return {
        "total_tickets": performance_metrics["total_tickets"],
        "success_rate": f"{success_rate}%",
        "average_satisfaction": performance_metrics["average_satisfaction"],
        "recommendations": generate_recommendations(),
        "timestamp": datetime.now().isoformat()
    }

def generate_recommendations():
    recommendations = []
    
    if performance_metrics["average_satisfaction"] < 3.0:
        recommendations.append("Improve response quality and accuracy")
    
    if performance_metrics["total_tickets"] > 0:
        success_rate = performance_metrics["successful_resolutions"] / performance_metrics["total_tickets"]
        if success_rate < 0.8:
            recommendations.append("Enhance knowledge base with more solutions")
    
    if not recommendations:
        recommendations.append("System performing well, continue monitoring")
    
    return recommendations

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8003)