@echo off
echo Starting Auto-resolve Service Desk - All Agents
echo ================================================

echo Starting Python Agents...
start "Triage Agent" cmd /k "cd agents\triage-agent && python main.py"
timeout /t 2 /nobreak >nul

start "Knowledge Agent" cmd /k "cd agents\knowledge-agent && python main.py"
timeout /t 2 /nobreak >nul

start "Learning Agent" cmd /k "cd agents\learning-agent && python main.py"
timeout /t 2 /nobreak >nul

echo Starting Node.js Agents...
start "Escalation Agent" cmd /k "cd agents\escalation-agent && npm run start:dev"
timeout /t 3 /nobreak >nul

start "Analytics Agent" cmd /k "cd agents\analytics-agent && npm run start:dev"
timeout /t 3 /nobreak >nul

echo Starting .NET Agent...
start "Automation Agent" cmd /k "cd agents\automation-agent\AutomationAgent.Api && dotnet run"
timeout /t 3 /nobreak >nul

echo Starting Dashboard...
start "Dashboard" dashboard\index.html

echo.
echo All agents started! Check individual windows for status.
echo Dashboard available at: dashboard\index.html
echo.
echo To test the system, run: node scripts\test-e2e.js
pause