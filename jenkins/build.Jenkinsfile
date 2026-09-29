// Build pipeline: the versioned builder zip (dist/Installer-<version>.zip), archived on the build page. Consumers unpack
// it on their build host and run `dotnet Installer.Cli.dll build installer.json`. Tests are jenkins/test.Jenkinsfile.
// Setup: docs/ci-jenkins.md.
pipeline {
    agent { label 'dotnet10 && macos' }

    options {
        timestamps()
        disableConcurrentBuilds()
        timeout(time: 30, unit: 'MINUTES')
        buildDiscarder(logRotator(numToKeepStr: '30', artifactNumToKeepStr: '10'))
    }

    environment {
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_NOLOGO = '1'
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
        DOTNET_CLI_HOME = "${WORKSPACE}/.dotnet-home"
        PATH = "/opt/homebrew/bin:/usr/local/bin:/usr/local/share/dotnet:${HOME}/.dotnet:${PATH}"
    }

    stages {
        stage('Clean') {
            steps { pwsh 'scripts/Clean.ps1' }
        }
        stage('Build') {
            steps { pwsh 'scripts/Build.ps1 -Configuration Release -BuildNumber $env:BUILD_NUMBER' }
        }
        stage('Publish') {
            steps { pwsh 'scripts/Publish.ps1 -Configuration Release -BuildNumber $env:BUILD_NUMBER' }
        }
    }

    post {
        success {
            archiveArtifacts artifacts: 'dist/*.zip, dist/version.txt', fingerprint: true
            script {
                currentBuild.displayName = "#${env.BUILD_NUMBER} ${readFile('dist/version.txt').trim()}"
            }
        }
    }
}
