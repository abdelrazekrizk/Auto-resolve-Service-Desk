# Create Azure AI Search Index - knowledge-base
# Run this script to create the search index for the demo

param(
    [Parameter(Mandatory=$true)]
    [string]$SearchServiceName,
    
    [Parameter(Mandatory=$true)]
    [string]$AdminKey
)

Write-Host "🔍 Creating Azure AI Search Index: knowledge-base" -ForegroundColor Green
Write-Host "=" -repeat 50

$searchEndpoint = "https://$SearchServiceName.search.windows.net"
$indexName = "knowledge-base"

# Headers for API calls
$headers = @{
    'Content-Type' = 'application/json'
    'api-key' = $AdminKey
}

# Read index schema
$indexSchema = Get-Content -Path "demo-data\knowledge-base-index.json" -Raw

try {
    # Create the index
    Write-Host "📋 Creating index schema..." -ForegroundColor Yellow
    
    $createIndexUri = "$searchEndpoint/indexes/$indexName" + "?api-version=2023-11-01"
    $response = Invoke-RestMethod -Uri $createIndexUri -Method PUT -Headers $headers -Body $indexSchema
    
    Write-Host "✅ Index '$indexName' created successfully!" -ForegroundColor Green
    
    # Wait a moment for index to be ready
    Start-Sleep -Seconds 3
    
    # Upload sample data
    Write-Host "📊 Uploading sample knowledge base data..." -ForegroundColor Yellow
    
    $sampleData = Get-Content -Path "demo-data\knowledge-base-data.json" -Raw
    $uploadUri = "$searchEndpoint/indexes/$indexName/docs/index" + "?api-version=2023-11-01"
    
    $uploadResponse = Invoke-RestMethod -Uri $uploadUri -Method POST -Headers $headers -Body $sampleData
    
    Write-Host "✅ Sample data uploaded successfully!" -ForegroundColor Green
    Write-Host "📈 Documents indexed: $($uploadResponse.value.Count)" -ForegroundColor Cyan
    
    # Test search
    Write-Host "🔍 Testing search functionality..." -ForegroundColor Yellow
    
    $testQuery = "ssl certificate"
    $searchUri = "$searchEndpoint/indexes/$indexName/docs" + "?api-version=2023-11-01&search=$testQuery&`$top=3"
    
    $searchResponse = Invoke-RestMethod -Uri $searchUri -Method GET -Headers $headers
    
    Write-Host "✅ Search test successful!" -ForegroundColor Green
    Write-Host "🎯 Found $($searchResponse.value.Count) results for '$testQuery'" -ForegroundColor Cyan
    
    foreach ($result in $searchResponse.value) {
        Write-Host "   • $($result.title) (Score: $([math]::Round($result.'@search.score', 2)))" -ForegroundColor White
    }
    
    Write-Host "`n🏆 Knowledge Base Index Setup Complete!" -ForegroundColor Green
    Write-Host "🔧 Index Name: $indexName" -ForegroundColor Cyan
    Write-Host "📊 Documents: 10 sample knowledge articles" -ForegroundColor Cyan
    Write-Host "🎯 Ready for demo integration!" -ForegroundColor Cyan
    
} catch {
    Write-Host "❌ Error creating index: $($_.Exception.Message)" -ForegroundColor Red
    
    if ($_.Exception.Response) {
        $errorResponse = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($errorResponse)
        $errorBody = $reader.ReadToEnd()
        Write-Host "Error details: $errorBody" -ForegroundColor Red
    }
}

Write-Host "`n📋 Next Steps:" -ForegroundColor Yellow
Write-Host "1. Update azure-config.json with your search service endpoint" -ForegroundColor White
Write-Host "2. Run: python demo_orchestrator.py" -ForegroundColor White
Write-Host "3. Test Knowledge Agent search functionality" -ForegroundColor White