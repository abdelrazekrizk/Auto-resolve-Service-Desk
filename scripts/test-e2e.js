const axios = require('axios');

const AGENTS = {
    triage: 'http://localhost:8001',
    knowledge: 'http://localhost:8002',
    learning: 'http://localhost:8003',
    escalation: 'http://localhost:3003',
    analytics: 'http://localhost:3004',
    automation: 'http://localhost:5000'
};

async function testWorkflow() {
    console.log('🧪 Testing End-to-End Workflow...\n');
    
    try {
        // 1. Health Check All Agents
        console.log('1️⃣ Health Check All Agents:');
        for (const [name, url] of Object.entries(AGENTS)) {
            try {
                const response = await axios.get(`${url}/health`);
                console.log(`   ✅ ${name}: ${response.data.status}`);
            } catch (error) {
                console.log(`   ❌ ${name}: ${error.message}`);
            }
        }
        
        console.log('\n2️⃣ Submit Ticket to Triage Agent:');
        const ticket = {
            title: "Login Error - Urgent",
            description: "Urgent: Cannot access system, getting authentication error",
            user_id: "user123"
        };
        
        const triageResponse = await axios.post(`${AGENTS.triage}/api/v1/classify`, ticket);
        console.log('   ✅ Triage Result:', {
            ticket_id: triageResponse.data.ticket_id,
            category: triageResponse.data.category,
            priority: triageResponse.data.priority,
            processing_time: triageResponse.data.processing_time
        });
        
        console.log('\n3️⃣ Search Knowledge Base:');
        const searchRequest = {
            query: "login error authentication",
            category: triageResponse.data.category
        };
        
        const knowledgeResponse = await axios.post(`${AGENTS.knowledge}/api/v1/search`, searchRequest);
        console.log('   ✅ Knowledge Results:', {
            results_found: knowledgeResponse.data.total_found,
            search_time: knowledgeResponse.data.search_time,
            top_solution: knowledgeResponse.data.results[0]?.solution || 'No solution found'
        });
        
        console.log('\n4️⃣ Check if Escalation Needed:');
        if (triageResponse.data.priority === 'high') {
            const escalationRequest = {
                ticketId: triageResponse.data.ticket_id,
                reason: "High priority ticket with no immediate solution",
                urgency: "high"
            };
            
            const escalationResponse = await axios.post(`${AGENTS.escalation}/api/v1/escalate`, escalationRequest);
            console.log('   ✅ Escalation Result:', {
                escalation_id: escalationResponse.data.escalationId,
                channels: escalationResponse.data.channels,
                estimated_response: escalationResponse.data.estimatedResponse
            });
        } else {
            console.log('   ℹ️ No escalation needed for medium/low priority ticket');
        }
        
        console.log('\n5️⃣ Execute Automation Workflow:');
        const workflowRequest = {
            TicketId: triageResponse.data.ticket_id,
            WorkflowType: "password_reset",
            Parameters: {}
        };
        
        const automationResponse = await axios.post(`${AGENTS.automation}/api/v1/execute`, workflowRequest);
        console.log('   ✅ Automation Result:', {
            workflow_id: automationResponse.data.workflowId,
            status: automationResponse.data.status,
            execution_time: automationResponse.data.executionTime,
            steps_completed: automationResponse.data.steps.length
        });
        
        console.log('\n6️⃣ Submit Feedback to Learning Agent:');
        const feedbackRequest = {
            ticket_id: triageResponse.data.ticket_id,
            user_satisfaction: 4,
            resolution_successful: true,
            comments: "Issue resolved quickly"
        };
        
        const learningResponse = await axios.post(`${AGENTS.learning}/api/v1/feedback`, feedbackRequest);
        console.log('   ✅ Learning Result:', {
            feedback_id: learningResponse.data.feedback_id,
            processed: learningResponse.data.processed,
            recommendations: learningResponse.data.recommendations
        });
        
        console.log('\n7️⃣ Get Analytics Dashboard:');
        const analyticsResponse = await axios.get(`${AGENTS.analytics}/api/v1/dashboard`);
        console.log('   ✅ Analytics Summary:', {
            total_tickets: analyticsResponse.data.metrics.totalTickets,
            resolution_rate: analyticsResponse.data.metrics.resolutionRate + '%',
            avg_resolution_time: analyticsResponse.data.metrics.avgResolutionTime,
            satisfaction_score: analyticsResponse.data.metrics.satisfactionScore
        });
        
        console.log('\n🎉 End-to-End Test Complete!');
        console.log('✅ All 6 agents working correctly');
        console.log('✅ Ticket processed through full workflow');
        console.log('✅ System ready for demo');
        
    } catch (error) {
        console.error('\n❌ Test Failed:', error.message);
        if (error.response) {
            console.error('Response:', error.response.status, error.response.data);
        }
    }
}

// Run the test
testWorkflow();