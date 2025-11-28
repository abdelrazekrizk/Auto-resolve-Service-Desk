"""
Demo Setup Script
Installs required packages and configures demo environment
"""

import subprocess
import sys
import os
import json
from pathlib import Path

def install_package(package):
    """Install Python package"""
    try:
        subprocess.check_call([sys.executable, "-m", "pip", "install", package])
        return True
    except subprocess.CalledProcessError:
        return False

def setup_demo_environment():
    """Setup complete demo environment"""
    print("🚀 Setting up Auto-resolve Service Desk Demo")
    print("=" * 50)
    
    # Required packages for demo
    required_packages = [
        "azure-identity",
        "azure-keyvault-secrets", 
        "azure-search-documents",
        "azure-servicebus",
        "openai",
        "asyncio",
        "aiohttp"
    ]
    
    print("📦 Installing required packages...")
    failed_packages = []
    
    for package in required_packages:
        print(f"   Installing {package}...")
        if install_package(package):
            print(f"   ✅ {package} installed successfully")
        else:
            print(f"   ❌ Failed to install {package}")
            failed_packages.append(package)
    
    if failed_packages:
        print(f"\n⚠️  Failed to install: {', '.join(failed_packages)}")
        print("Please install manually using: pip install <package_name>")
    else:
        print("\n✅ All packages installed successfully!")
    
    # Create directory structure
    print("\n📁 Creating directory structure...")
    directories = [
        "demo-data",
        "agents/triage-agent",
        "agents/knowledge-agent", 
        "agents/automation-agent",
        "agents/escalation-agent",
        "agents/learning-agent",
        "agents/analytics-agent",
        "shared",
        "dashboard"
    ]
    
    for directory in directories:
        Path(directory).mkdir(parents=True, exist_ok=True)
        print(f"   ✅ Created {directory}/")
    
    # Configuration check
    print("\n🔧 Configuration Check...")
    config_file = "demo-data/azure-config.json"
    
    if os.path.exists(config_file):
        print("   ✅ Azure configuration file found")
        
        # Validate configuration
        try:
            with open(config_file, 'r') as f:
                config = json.load(f)
            
            required_services = ["openai", "ai_search", "service_bus", "key_vault"]
            missing_services = []
            
            for service in required_services:
                if service not in config.get("azure_services", {}):
                    missing_services.append(service)
            
            if missing_services:
                print(f"   ⚠️  Missing configuration for: {', '.join(missing_services)}")
            else:
                print("   ✅ All Azure services configured")
                
        except json.JSONDecodeError:
            print("   ❌ Invalid JSON in configuration file")
    else:
        print("   ⚠️  Azure configuration file not found")
        print("   Please update demo-data/azure-config.json with your Azure service details")
    
    # Demo data check
    print("\n📊 Demo Data Check...")
    demo_data_file = "demo-data/mock-tickets.json"
    
    if os.path.exists(demo_data_file):
        try:
            with open(demo_data_file, 'r') as f:
                data = json.load(f)
            
            ticket_count = len(data.get("tickets", []))
            scenario_count = len(data.get("scenarios", []))
            
            print(f"   ✅ {ticket_count} demo tickets loaded")
            print(f"   ✅ {scenario_count} demo scenarios available")
            
        except json.JSONDecodeError:
            print("   ❌ Invalid JSON in demo data file")
    else:
        print("   ❌ Demo data file not found")
    
    # Environment validation
    print("\n🔍 Environment Validation...")
    
    # Check Python version
    python_version = sys.version_info
    if python_version >= (3, 8):
        print(f"   ✅ Python {python_version.major}.{python_version.minor}.{python_version.micro}")
    else:
        print(f"   ⚠️  Python {python_version.major}.{python_version.minor} (3.8+ recommended)")
    
    # Check Azure CLI (optional)
    try:
        result = subprocess.run(["az", "--version"], capture_output=True, text=True)
        if result.returncode == 0:
            print("   ✅ Azure CLI available")
        else:
            print("   ⚠️  Azure CLI not found (optional)")
    except FileNotFoundError:
        print("   ⚠️  Azure CLI not found (optional)")
    
    # Final status
    print("\n🎯 Setup Complete!")
    print("-" * 30)
    
    if not failed_packages:
        print("✅ Environment ready for demo")
        print("\nNext steps:")
        print("1. Update azure-config.json with your Azure service details")
        print("2. Run: python demo_orchestrator.py")
        print("3. Or run specific scenario: python demo_orchestrator.py 'Peak Load Simulation'")
    else:
        print("⚠️  Please resolve package installation issues before running demo")
    
    return len(failed_packages) == 0

if __name__ == "__main__":
    setup_demo_environment()