# Steam Runtime Build Script for Linux
# Creates a Steam-compatible Linux build for Kriss Journey

$ErrorActionPreference = "Stop"
$ProjectRoot = "D:\KrissJourney"
$ScriptsRoot = Join-Path -Path $ProjectRoot -ChildPath "Scripts"
$BuildRoot = Join-Path -Path $ScriptsRoot -ChildPath "Linux-SteamOS"
$OutputDir = Join-Path -Path $BuildRoot -ChildPath "output"
$DockerfilePath = Join-Path -Path $BuildRoot -ChildPath "Dockerfile.steamrtpublish"
$ImageName = "kriss-journey-steamrt"
$ImageTag = "latest"
$FullImageName = "${ImageName}:${ImageTag}"


Write-Host "Checking Docker installation..." -ForegroundColor Cyan

# Function to check if Docker is running
function Test-DockerRunning {
    try {
        docker info > $null 2>&1
        return $true
    }
    catch {
        return $false
    }
}

# Check if Docker is in PATH
$dockerInPath = Get-Command docker -ErrorAction SilentlyContinue
if ($dockerInPath) {
    Write-Host "Docker found in PATH at: $($dockerInPath.Source)" -ForegroundColor Green
    
    # Check if Docker is running
    if (Test-DockerRunning) {
        Write-Host "Docker service is running properly." -ForegroundColor Green
    }
    else {
        Write-Host "Docker is installed but not running." -ForegroundColor Yellow
        Write-Host "Please start Docker Desktop and wait until the whale icon in the system tray shows 'Docker Desktop is running'." -ForegroundColor Yellow
        Write-Host "After Docker is running, try running this script again." -ForegroundColor Yellow
        exit 1
    }
}
else {
    # Check common Docker installation paths
    $commonPaths = @(
        "C:\Program Files\Docker\Docker\resources\bin\docker.exe",
        "${env:ProgramFiles}\Docker\Docker\resources\bin\docker.exe",
        "${env:ProgramW6432}\Docker\Docker\resources\bin\docker.exe",
        "${env:LOCALAPPDATA}\Docker\Docker\resources\bin\docker.exe"
    )
    
    $dockerPath = $null
    foreach ($path in $commonPaths) {
        if (Test-Path $path) {
            $dockerPath = $path
            break
        }
    }
    
    if ($dockerPath) {
        Write-Host "Docker found at $dockerPath but not in PATH" -ForegroundColor Yellow
        Write-Host "Temporarily adding Docker to PATH for this session..." -ForegroundColor Yellow
        
        $env:PATH += ";$(Split-Path -Parent $dockerPath)"
        
        # Check if Docker is running after adding to PATH
        if (Test-DockerRunning) {
            Write-Host "Docker service is running properly." -ForegroundColor Green
        }
        else {
            Write-Host "Docker is installed but not running." -ForegroundColor Yellow
            Write-Host "Please start Docker Desktop and wait until the whale icon in the system tray shows 'Docker Desktop is running'." -ForegroundColor Yellow
            Write-Host "After Docker is running, try running this script again." -ForegroundColor Yellow
            exit 1
        }
    }
    else {
        Write-Host "Docker installation not found." -ForegroundColor Red
        Write-Host "Please ensure Docker Desktop is installed and running." -ForegroundColor Red
        Write-Host "Download from: https://www.docker.com/products/docker-desktop/" -ForegroundColor Cyan
        Write-Host "After installation, you may need to REBOOT YOUR COMPUTER to complete setup." -ForegroundColor Yellow
        exit 1
    }
}

Write-Host "Cleaning previous build output..." -ForegroundColor Yellow
Remove-Item -Path "$OutputDir\*" -Recurse -Force

# Create output directory if it doesn't exist
if (-not (Test-Path $OutputDir)) {
    New-Item -Path $OutputDir -ItemType Directory -Force | Out-Null
}

# Build the Docker image
Write-Host "Building Steam Runtime Docker image..." -ForegroundColor Cyan
docker build -t kriss-journey-steamrt -f $DockerfilePath $ProjectRoot

# Run the container to build the application
Write-Host "Building Kriss Journey inside Steam Runtime container..." -ForegroundColor Cyan
docker run --rm -v "${OutputDir}:/output" kriss-journey-steamrt

# Check if build was successful
if (Test-Path (Join-Path -Path $OutputDir -ChildPath "Kriss")) {
    Write-Host "Build successful! Output is available in: $OutputDir" -ForegroundColor Green
        
    # Copy the krissLauncher.sh script to the output directory
    $launcherSource = Join-Path -Path $BuildRoot -ChildPath "krissLauncher.sh"
    $launcherTarget = Join-Path -Path $OutputDir -ChildPath "krissLauncher.sh"
        
    # Copy the launcher script
    if (Test-Path $launcherSource) {
        Write-Host "Copying launcher script to output directory..." -ForegroundColor Cyan
        Copy-Item -Path $launcherSource -Destination $launcherTarget -Force
            
        # Ensure the launcher script has the correct line endings (LF instead of CRLF)
        $content = Get-Content -Path $launcherTarget -Raw
        $content = $content -replace "`r`n", "`n"
        [System.IO.File]::WriteAllText($launcherTarget, $content)
    }
    else {
        Write-Host "WARNING: Could not find launcher script at $launcherSource" -ForegroundColor Yellow
    }
        
    # Define the path for the new ZIP file
    $zipFileName = "Kriss-linux-x64.zip"
    $zipPath = Join-Path -Path $OutputDir -ChildPath $zipFileName
        
    # Remove any existing ZIP file with the same name in the output directory before creating a new one
    if (Test-Path $zipPath) {
        Write-Host "Removing existing ZIP file: $zipPath" -ForegroundColor Yellow
        Remove-Item -Path $zipPath -Force
    }
    
    # Get all items (files and directories) in $OutputDir to be included in the ZIP
    # Exclude the ZIP file we are about to create itself
    $itemsToArchive = Get-ChildItem -Path $OutputDir | Where-Object { $_.Name -ne $zipFileName }
        
    if ($itemsToArchive.Count -eq 0) {
        Write-Error "No items found in $OutputDir to archive. Build might have failed to produce output."
        # You might want to throw an error here or ensure the script exits
        exit 1 
    }
    
    Write-Host "Items to be archived from $OutputDir" -ForegroundColor Cyan
    $itemsToArchive | ForEach-Object { Write-Host "- $($_.FullName)" }
    
    # Create the ZIP file
    Compress-Archive -Path $itemsToArchive.FullName -DestinationPath $zipPath -Force
    Write-Host "Successfully created archive: $zipPath" -ForegroundColor Green
        
    # Remove uncompressed original files from $OutputDir after zipping
    Write-Host "Removing original uncompressed files from $OutputDir..." -ForegroundColor Cyan
    foreach ($item in $itemsToArchive) {
        if (Test-Path $item.FullName) {
            Write-Host "Removing: $($item.FullName)"
            Remove-Item -Path $item.FullName -Recurse -Force
        }
    }
        
    Write-Host "Created Steam Linux distribution package: $zipPath" -ForegroundColor Green
    
    # Move the ZIP to the central Scripts/.output directory
    $finalOutputDir = Join-Path -Path $ScriptsRoot -ChildPath ".output"
    if (-not (Test-Path $finalOutputDir)) {
        New-Item -Path $finalOutputDir -ItemType Directory -Force | Out-Null
    }
    $finalZip = Join-Path -Path $finalOutputDir -ChildPath "Kriss-linux-x64.zip"
    Move-Item -Path $zipPath -Destination $finalZip -Force
    Write-Host "Moved Steam Linux ZIP to: $finalZip" -ForegroundColor Green
} 
else {
    Write-Host "Build failed! Check the Docker output for errors." -ForegroundColor Red
}