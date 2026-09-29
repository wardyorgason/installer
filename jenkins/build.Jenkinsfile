// Build pipeline: the versioned builder zip (dist/Installer-<version>.zip), archived on the build page for every branch.
// jenkins/release.Jenkinsfile publishes a chosen build's zip as a GitHub release, by hand. Tests are
// jenkins/test.Jenkinsfile. Setup: docs/ci-jenkins.md.
pipeline {
    agent { label 'dotnet10 && macos' }

    options {
        timestamps()
        disableConcurrentBuilds()
        timeout(time: 30, unit: 'MINUTES')
        buildDiscarder(logRotator(numToKeepStr: '30', artifactNumToKeepStr: '10'))
        // The installer-release job copies these artifacts (Copy Artifact plugin).
        copyArtifactPermission('installer-release')
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
            archiveArtifacts artifacts: 'dist/*.zip, dist/*.zip.sha256, dist/version.txt, dist/release.json', fingerprint: true
            script {
                currentBuild.displayName = "#${env.BUILD_NUMBER} ${readFile('dist/version.txt').trim()}"
            }
        }
    }
}
