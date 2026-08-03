param(
    [string]$BaseUrl = 'http://localhost:5080',
    [string]$Email = 'reader@bookspace.local',
    [string]$Password = 'Reader123!'
)

$ErrorActionPreference = 'Stop'

function Invoke-BookSpaceRequest {
    param(
        [Parameter(Mandatory)]
        [string]$Method,
        [Parameter(Mandatory)]
        [string]$Path,
        [object]$Body,
        [string]$AccessToken
    )

    $parameters = @{
        Method = $Method
        Uri = "$BaseUrl$Path"
        ContentType = 'application/json'
    }

    if ($null -ne $Body) {
        $jsonBody = $Body | ConvertTo-Json -Depth 8
        $parameters.Body = [System.Text.Encoding]::UTF8.GetBytes($jsonBody)
    }

    if ($AccessToken) {
        $parameters.Headers = @{ Authorization = "Bearer $AccessToken" }
    }

    Invoke-RestMethod @parameters
}

function Invoke-BookSpaceExpectedError {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [string]$AccessToken
    )

    try {
        $payload = Invoke-BookSpaceRequest `
            -Method Get `
            -Path $Path `
            -AccessToken $AccessToken

        return [pscustomobject]@{
            StatusCode = 200
            Payload = $payload
        }
    }
    catch {
        $errorRecord = $_
        $response = $errorRecord.Exception.Response
        $statusCode = $null
        $rawBody = [string]$errorRecord.ErrorDetails.Message

        if ($null -ne $response -and $response.PSObject.Properties.Name -contains 'StatusCode') {
            $statusCode = [int]$response.StatusCode
        }

        if (
            [string]::IsNullOrWhiteSpace($rawBody) -and
            $null -ne $response -and
            $response.PSObject.Methods.Name -contains 'GetResponseStream'
        ) {
            $stream = $response.GetResponseStream()
            if ($null -ne $stream) {
                $reader = New-Object System.IO.StreamReader($stream)
                try {
                    $rawBody = $reader.ReadToEnd()
                }
                finally {
                    $reader.Dispose()
                }
            }
        }

        $payload = $null
        if (-not [string]::IsNullOrWhiteSpace($rawBody)) {
            try {
                $payload = $rawBody | ConvertFrom-Json
            }
            catch {
                $payload = $null
            }
        }

        return [pscustomobject]@{
            StatusCode = $statusCode
            Payload = $payload
        }
    }
}

$healthCorrelationId = "bookspace-smoke-$([Guid]::NewGuid().ToString('N'))"
$healthResponse = Invoke-WebRequest `
    -UseBasicParsing `
    -Method Get `
    -Uri "$BaseUrl/health" `
    -Headers @{ 'X-Correlation-ID' = $healthCorrelationId }
$health = ([string]$healthResponse.Content).Trim()
$returnedHealthCorrelationId = [string]$healthResponse.Headers['X-Correlation-ID']
if (
    [int]$healthResponse.StatusCode -ne 200 -or
    $health -ne 'Healthy' -or
    $returnedHealthCorrelationId -ne $healthCorrelationId
) {
    throw "Health/correlation check không hợp lệ: status=$($healthResponse.StatusCode), body=$health, correlation=$returnedHealthCorrelationId"
}

$unauthorizedRecommendations = Invoke-BookSpaceExpectedError `
    -Path '/api/books/recommendations?page=1&pageSize=12'
$unauthorizedOnboarding = Invoke-BookSpaceExpectedError `
    -Path '/api/users/me/onboarding'
$externalCatalog = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/external-books/search?query=clean%20code&limit=3'

$login = Invoke-BookSpaceRequest `
    -Method Post `
    -Path '/api/auth/login' `
    -Body @{ email = $Email; password = $Password }

if (-not $login.success -or -not $login.data.accessToken) {
    throw 'Đăng nhập smoke test không thành công.'
}

$token = $login.data.accessToken
$onboardingCategories = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/categories?page=1&pageSize=100' `
    -AccessToken $token
$onboardingBooks = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/books?page=1&pageSize=100' `
    -AccessToken $token

$onboardingBookItems = @($onboardingBooks.data.items)
$referenceBookIds = @(
    $onboardingBookItems |
        Select-Object -First 3 |
        ForEach-Object { [string]$_.id }
)
$onboardingCandidateBooks = @(
    $onboardingBookItems |
        Where-Object { [string]$_.id -notin $referenceBookIds }
)
$candidateCategoryIds = @(
    $onboardingCandidateBooks |
        ForEach-Object { @($_.categories) } |
        ForEach-Object { [string]$_.id } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Sort-Object -Unique
)
$allActiveCategoryIds = @(
    $onboardingCategories.data.items |
        ForEach-Object { [string]$_.id } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
)
$preferredCategoryIds = @($candidateCategoryIds | Select-Object -First 3)
if ($preferredCategoryIds.Count -lt 3) {
    $preferredCategoryIds += @(
        $allActiveCategoryIds |
            Where-Object { $_ -notin $preferredCategoryIds } |
            Select-Object -First (3 - $preferredCategoryIds.Count)
    )
}

if (
    -not $onboardingCategories.success -or
    -not $onboardingBooks.success -or
    $preferredCategoryIds.Count -ne 3 -or
    $referenceBookIds.Count -ne 3
) {
    throw 'Catalog active không có đủ 3 thể loại và 3 sách cho onboarding smoke.'
}

$onboardingSuffix = [Guid]::NewGuid().ToString('N')
$onboardingRegistration = Invoke-BookSpaceRequest `
    -Method Post `
    -Path '/api/auth/register' `
    -Body @{
        email = "onboarding-smoke-$onboardingSuffix@bookspace.local"
        password = 'Reader123!'
        displayName = "Onboarding Smoke $($onboardingSuffix.Substring(0, 8))"
    }
if (-not $onboardingRegistration.success -or -not $onboardingRegistration.data.accessToken) {
    throw 'Đăng ký tài khoản onboarding smoke không thành công.'
}
$onboardingToken = $onboardingRegistration.data.accessToken
$onboardingInitial = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/users/me/onboarding' `
    -AccessToken $onboardingToken
$onboardingDraft = Invoke-BookSpaceRequest `
    -Method Put `
    -Path '/api/users/me/onboarding' `
    -Body @{
        preferredCategoryIds = $preferredCategoryIds
        referenceBookIds = $referenceBookIds
    } `
    -AccessToken $onboardingToken
$onboardingCompleted = Invoke-BookSpaceRequest `
    -Method Post `
    -Path '/api/users/me/onboarding/complete' `
    -AccessToken $onboardingToken
$onboardingReadback = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/users/me/onboarding' `
    -AccessToken $onboardingToken
$onboardingRecommendations = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/books/recommendations?page=1&pageSize=100' `
    -AccessToken $onboardingToken

$skipSuffix = [Guid]::NewGuid().ToString('N')
$skipRegistration = Invoke-BookSpaceRequest `
    -Method Post `
    -Path '/api/auth/register' `
    -Body @{
        email = "onboarding-skip-smoke-$skipSuffix@bookspace.local"
        password = 'Reader123!'
        displayName = "Onboarding Skip $($skipSuffix.Substring(0, 8))"
    }
if (-not $skipRegistration.success -or -not $skipRegistration.data.accessToken) {
    throw 'Đăng ký tài khoản skip onboarding smoke không thành công.'
}
$skipToken = $skipRegistration.data.accessToken
$onboardingSkipped = Invoke-BookSpaceRequest `
    -Method Post `
    -Path '/api/users/me/onboarding/skip' `
    -AccessToken $skipToken
$onboardingSkippedReadback = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/users/me/onboarding' `
    -AccessToken $skipToken

$recommendations = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/books/recommendations?page=1&pageSize=12' `
    -AccessToken $token
$recommendationsRepeat = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/books/recommendations?page=1&pageSize=12' `
    -AccessToken $token
$feed = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/feed?type=READING&page=1&pageSize=10' `
    -AccessToken $token
$invalidFeedType = Invoke-BookSpaceExpectedError `
    -Path '/api/feed?type=UNKNOWN&page=1&pageSize=10' `
    -AccessToken $token
$people = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/users?search=H%C3%A0%20Linh&page=1&pageSize=20' `
    -AccessToken $token
$peopleSuggestions = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/users/suggestions?page=1&pageSize=20' `
    -AccessToken $token
$books = Invoke-BookSpaceRequest -Method Get -Path '/api/books?page=1&pageSize=8' -AccessToken $token
$dashboard = Invoke-BookSpaceRequest -Method Get -Path '/api/dashboard' -AccessToken $token
$library = Invoke-BookSpaceRequest -Method Get -Path '/api/library?page=1&pageSize=20' -AccessToken $token
$adminLogin = Invoke-BookSpaceRequest `
    -Method Post `
    -Path '/api/auth/login' `
    -Body @{ email = 'admin@bookspace.local'; password = 'Admin123!' }
if (-not $adminLogin.success -or -not $adminLogin.data.accessToken) {
    throw 'Đăng nhập admin cho cold-start recommendation không thành công.'
}
$adminToken = $adminLogin.data.accessToken
$moderationSuffix = [Guid]::NewGuid().ToString('N')
$moderationTarget = Invoke-BookSpaceRequest `
    -Method Post `
    -Path '/api/auth/register' `
    -Body @{
        email = "moderation-smoke-$moderationSuffix@bookspace.local"
        password = 'Reader123!'
        displayName = "Moderation Smoke $($moderationSuffix.Substring(0, 8))"
    }
$contentReport = Invoke-BookSpaceRequest `
    -Method Post `
    -Path '/api/reports' `
    -Body @{
        targetType = 'USER'
        targetId = $moderationTarget.data.user.id
        reason = 'OTHER'
        details = 'Báo cáo smoke dùng để xác minh hàng đợi kiểm duyệt.'
    } `
    -AccessToken $token
$moderationQueue = Invoke-BookSpaceRequest `
    -Method Get `
    -Path "/api/admin/reports?status=PENDING&targetType=USER&page=1&pageSize=100" `
    -AccessToken $adminToken
$moderationResolution = Invoke-BookSpaceRequest `
    -Method Patch `
    -Path "/api/admin/reports/$($contentReport.data.id)/resolution" `
    -Body @{
        status = 'DISMISSED'
        action = 'NONE'
        resolutionNote = 'Đã xác minh đường đi smoke của Community Safety.'
    } `
    -AccessToken $adminToken
$safetyMute = Invoke-BookSpaceRequest `
    -Method Post `
    -Path "/api/users/$($moderationTarget.data.user.id)/mute" `
    -AccessToken $token
$safetyListMuted = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/users/me/safety?page=1&pageSize=100' `
    -AccessToken $token
$profileWhileMuted = Invoke-BookSpaceRequest `
    -Method Get `
    -Path "/api/users/$($moderationTarget.data.user.id)" `
    -AccessToken $token
$safetyUnmute = Invoke-BookSpaceRequest `
    -Method Delete `
    -Path "/api/users/$($moderationTarget.data.user.id)/mute" `
    -AccessToken $token
$safetyFollow = Invoke-BookSpaceRequest `
    -Method Post `
    -Path "/api/users/$($moderationTarget.data.user.id)/follow" `
    -AccessToken $token
$safetyBlock = Invoke-BookSpaceRequest `
    -Method Post `
    -Path "/api/users/$($moderationTarget.data.user.id)/block" `
    -AccessToken $token
$profileWhileBlocked = Invoke-BookSpaceExpectedError `
    -Path "/api/users/$($moderationTarget.data.user.id)" `
    -AccessToken $token
$safetyListBlocked = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/users/me/safety?page=1&pageSize=100' `
    -AccessToken $token
$safetyUnblock = Invoke-BookSpaceRequest `
    -Method Delete `
    -Path "/api/users/$($moderationTarget.data.user.id)/block" `
    -AccessToken $token
$profileAfterUnblock = Invoke-BookSpaceRequest `
    -Method Get `
    -Path "/api/users/$($moderationTarget.data.user.id)" `
    -AccessToken $token
$directMessageReaderFollow = Invoke-BookSpaceRequest `
    -Method Post `
    -Path "/api/users/$($moderationTarget.data.user.id)/follow" `
    -AccessToken $token
$directMessageTargetFollow = Invoke-BookSpaceRequest `
    -Method Post `
    -Path "/api/users/$($login.data.user.id)/follow" `
    -AccessToken $moderationTarget.data.accessToken
$directConversation = Invoke-BookSpaceRequest `
    -Method Post `
    -Path '/api/conversations' `
    -Body @{ targetUserId = $moderationTarget.data.user.id } `
    -AccessToken $token
$directMessageContent = "Direct Message smoke $([Guid]::NewGuid().ToString('N'))"
$directMessage = Invoke-BookSpaceRequest `
    -Method Post `
    -Path "/api/conversations/$($directConversation.data.id)/messages" `
    -Body @{ content = $directMessageContent } `
    -AccessToken $token
$directMessageHistory = Invoke-BookSpaceRequest `
    -Method Get `
    -Path "/api/conversations/$($directConversation.data.id)/messages?pageSize=30" `
    -AccessToken $moderationTarget.data.accessToken
$directMessageUnread = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/conversations/unread-count' `
    -AccessToken $moderationTarget.data.accessToken
$directMessageReadState = Invoke-BookSpaceRequest `
    -Method Post `
    -Path "/api/conversations/$($directConversation.data.id)/read" `
    -Body @{ lastReadMessageId = $directMessage.data.id } `
    -AccessToken $moderationTarget.data.accessToken
$bookListName = "Book list smoke $([Guid]::NewGuid().ToString('N'))"
$bookList = Invoke-BookSpaceRequest `
    -Method Post `
    -Path '/api/book-lists' `
    -Body @{ name = $bookListName; description = 'Luồng kiểm tra bộ sưu tập'; visibility = 'PRIVATE' } `
    -AccessToken $token
$privateBookList = Invoke-BookSpaceExpectedError -Path "/api/book-lists/$($bookList.data.id)"
$publicBookListUpdate = Invoke-BookSpaceRequest `
    -Method Patch `
    -Path "/api/book-lists/$($bookList.data.id)" `
    -Body @{ name = $bookListName; description = 'Luồng kiểm tra bộ sưu tập'; visibility = 'PUBLIC' } `
    -AccessToken $token
$bookListAdd = Invoke-BookSpaceRequest `
    -Method Post `
    -Path "/api/book-lists/$($bookList.data.id)/books" `
    -Body @{ bookId = $books.data.items[0].id } `
    -AccessToken $token
$bookListReorder = Invoke-BookSpaceRequest `
    -Method Put `
    -Path "/api/book-lists/$($bookList.data.id)/books/reorder" `
    -Body @{ bookIds = @($books.data.items[0].id) } `
    -AccessToken $token
$bookListsMine = Invoke-BookSpaceRequest `
    -Method Get `
    -Path "/api/book-lists?page=1&pageSize=20&bookId=$($books.data.items[0].id)" `
    -AccessToken $token
$publicBookList = Invoke-BookSpaceRequest -Method Get -Path "/api/book-lists/$($bookList.data.id)"
$bookListDelete = Invoke-BookSpaceRequest `
    -Method Delete `
    -Path "/api/book-lists/$($bookList.data.id)" `
    -AccessToken $token
$deletedBookList = Invoke-BookSpaceExpectedError `
    -Path "/api/book-lists/$($bookList.data.id)" `
    -AccessToken $token
$adminLibrary = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/library?page=1&pageSize=20' `
    -AccessToken $adminToken
$adminRecommendations = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/books/recommendations?page=1&pageSize=12' `
    -AccessToken $adminToken
$adminRecommendationsRepeat = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/books/recommendations?page=1&pageSize=12' `
    -AccessToken $adminToken
$readingSessions = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/reading-sessions?page=1&pageSize=20' `
    -AccessToken $token
$activeReadingSession = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/reading-sessions/active' `
    -AccessToken $token
$goals = Invoke-BookSpaceRequest -Method Get -Path '/api/reading-goals?page=1&pageSize=20' -AccessToken $token
$notes = Invoke-BookSpaceRequest -Method Get -Path '/api/reading-notes?page=1&pageSize=20' -AccessToken $token
$clubs = Invoke-BookSpaceRequest -Method Get -Path '/api/clubs?page=1&pageSize=20' -AccessToken $token
$clubInvitations = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/clubs/invitations?page=1&pageSize=20' `
    -AccessToken $token
$clubDetail = $null
$clubMembers = $null
$clubSprints = $null
$sprintDetail = $null
$sprintLeaderboard = $null
$sprintTimeline = $null
$clubChatMessage = $null
$clubChatHistory = $null
$clubChatUnread = $null
$clubChatReadState = $null
if ($clubs.success -and $clubs.data.items.Count -gt 0) {
    $clubId = $clubs.data.items[0].id
    $clubDetail = Invoke-BookSpaceRequest -Method Get -Path "/api/clubs/$clubId" -AccessToken $token
    $clubMembers = Invoke-BookSpaceRequest `
        -Method Get `
        -Path "/api/clubs/$clubId/members?page=1&pageSize=20" `
        -AccessToken $token
    $clubSprints = Invoke-BookSpaceRequest `
        -Method Get `
        -Path "/api/clubs/$clubId/reading-sprints?page=1&pageSize=20" `
        -AccessToken $token
    if ($clubSprints.success -and $clubSprints.data.items.Count -gt 0) {
        $sprintId = $clubSprints.data.items[0].id
        $sprintDetail = Invoke-BookSpaceRequest `
            -Method Get `
            -Path "/api/clubs/$clubId/reading-sprints/$sprintId" `
            -AccessToken $token
        $sprintLeaderboard = Invoke-BookSpaceRequest `
            -Method Get `
            -Path "/api/clubs/$clubId/reading-sprints/$sprintId/leaderboard?page=1&pageSize=20" `
            -AccessToken $token
        $sprintTimeline = Invoke-BookSpaceRequest `
            -Method Get `
            -Path "/api/clubs/$clubId/reading-sprints/$sprintId/timeline?page=1&pageSize=20" `
            -AccessToken $token
    }

    $chatClub = @($clubs.data.items | Where-Object { $_.isJoined } | Select-Object -First 1)
    if ($chatClub.Count -eq 0) {
        $chatClub = @(
            $clubs.data.items |
                Where-Object { -not $_.isPrivate } |
                Select-Object -First 1
        )
        if ($chatClub.Count -eq 0) {
            throw 'Seed không có câu lạc bộ khả dụng để smoke Club Chat.'
        }

        $null = Invoke-BookSpaceRequest `
            -Method Post `
            -Path "/api/clubs/$($chatClub[0].id)/join" `
            -AccessToken $token
    }

    $chatClubId = $chatClub[0].id
    $chatContent = "Club Chat smoke $([Guid]::NewGuid().ToString('N'))"
    $clubChatMessage = Invoke-BookSpaceRequest `
        -Method Post `
        -Path "/api/clubs/$chatClubId/chat/messages" `
        -Body @{ content = $chatContent } `
        -AccessToken $token
    $clubChatHistory = Invoke-BookSpaceRequest `
        -Method Get `
        -Path "/api/clubs/$chatClubId/chat/messages?pageSize=30" `
        -AccessToken $token
    $clubChatUnread = Invoke-BookSpaceRequest `
        -Method Get `
        -Path "/api/clubs/$chatClubId/chat/unread-count" `
        -AccessToken $token
    $clubChatReadState = Invoke-BookSpaceRequest `
        -Method Post `
        -Path "/api/clubs/$chatClubId/chat/read" `
        -Body @{ lastReadMessageId = $clubChatMessage.data.id } `
        -AccessToken $token
}
$insightsOverview = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/insights/overview?days=30&utcOffsetMinutes=420' `
    -AccessToken $token
$insightsCalendar = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/insights/calendar?days=365&utcOffsetMinutes=420' `
    -AccessToken $token
$insightsWeekly = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/insights/weekly?weeks=12&utcOffsetMinutes=420' `
    -AccessToken $token
$insightsMonthly = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/insights/monthly?months=12&utcOffsetMinutes=420' `
    -AccessToken $token

if (
    -not $onboardingInitial.success -or
    -not $onboardingDraft.success -or
    -not $onboardingCompleted.success -or
    -not $onboardingReadback.success -or
    -not $onboardingRecommendations.success -or
    -not $onboardingSkipped.success -or
    -not $onboardingSkippedReadback.success -or
    -not $people.success -or
    -not $peopleSuggestions.success -or
    -not $recommendations.success -or
    -not $recommendationsRepeat.success -or
    -not $adminLibrary.success -or
    -not $adminRecommendations.success -or
    -not $adminRecommendationsRepeat.success -or
    -not $bookList.success -or
    -not $publicBookListUpdate.success -or
    -not $bookListAdd.success -or
    -not $bookListReorder.success -or
    -not $bookListsMine.success -or
    -not $publicBookList.success -or
    -not $bookListDelete.success -or
    -not $feed.success -or
    -not $books.success -or
    -not $dashboard.success -or
    -not $library.success -or
    -not $readingSessions.success -or
    -not $activeReadingSession.success -or
    -not $goals.success -or
    -not $notes.success -or
    -not $clubs.success -or
    -not $clubInvitations.success -or
    ($null -ne $clubDetail -and -not $clubDetail.success) -or
    ($null -ne $clubMembers -and -not $clubMembers.success) -or
    ($null -ne $clubSprints -and -not $clubSprints.success) -or
    ($null -ne $sprintDetail -and -not $sprintDetail.success) -or
    ($null -ne $sprintLeaderboard -and -not $sprintLeaderboard.success) -or
    ($null -ne $sprintTimeline -and -not $sprintTimeline.success) -or
    -not $insightsOverview.success -or
    -not $insightsCalendar.success -or
    -not $insightsWeekly.success -or
    -not $insightsMonthly.success
) {
    throw 'Một API lõi trả về envelope thất bại.'
}

if (
    $unauthorizedRecommendations.StatusCode -ne 401 -or
    $null -eq $unauthorizedRecommendations.Payload -or
    $unauthorizedRecommendations.Payload.success -ne $false -or
    $unauthorizedRecommendations.Payload.code -ne 'UNAUTHORIZED' -or
    $unauthorizedRecommendations.Payload.message -ne 'Bạn cần đăng nhập để tiếp tục.'
) {
    throw 'Recommendation không trả đúng envelope 401 UNAUTHORIZED tiếng Việt.'
}

if (
    $unauthorizedOnboarding.StatusCode -ne 401 -or
    $null -eq $unauthorizedOnboarding.Payload -or
    $unauthorizedOnboarding.Payload.success -ne $false -or
    $unauthorizedOnboarding.Payload.code -ne 'UNAUTHORIZED' -or
    $unauthorizedOnboarding.Payload.message -ne 'Bạn cần đăng nhập để tiếp tục.'
) {
    throw 'Onboarding không trả đúng envelope 401 UNAUTHORIZED tiếng Việt.'
}

$requiredOnboardingStateFields = @(
    'status',
    'finishedAt',
    'preferredCategoryIds',
    'referenceBookIds'
)
$onboardingStateShapeInvalid = $false
foreach ($onboardingState in @(
    $onboardingInitial.data,
    $onboardingDraft.data,
    $onboardingCompleted.data,
    $onboardingReadback.data,
    $onboardingSkipped.data,
    $onboardingSkippedReadback.data
)) {
    $onboardingStateFields = @($onboardingState.PSObject.Properties.Name)
    if (
        @(
            $requiredOnboardingStateFields |
                Where-Object { $_ -notin $onboardingStateFields }
        ).Count -gt 0 -or
        @(
            $onboardingStateFields |
                Where-Object { $_ -notin $requiredOnboardingStateFields }
        ).Count -gt 0 -or
        $null -eq $onboardingState.preferredCategoryIds -or
        $null -eq $onboardingState.referenceBookIds
    ) {
        $onboardingStateShapeInvalid = $true
        break
    }
}

$expectedPreferredCategorySignature = @(
    $preferredCategoryIds | Sort-Object
) -join '|'
$expectedReferenceBookSignature = @(
    $referenceBookIds | Sort-Object
) -join '|'
$draftPreferredCategorySignature = @(
    $onboardingDraft.data.preferredCategoryIds | ForEach-Object { [string]$_ } | Sort-Object
) -join '|'
$draftReferenceBookSignature = @(
    $onboardingDraft.data.referenceBookIds | ForEach-Object { [string]$_ } | Sort-Object
) -join '|'
$completedPreferredCategorySignature = @(
    $onboardingReadback.data.preferredCategoryIds | ForEach-Object { [string]$_ } | Sort-Object
) -join '|'
$completedReferenceBookSignature = @(
    $onboardingReadback.data.referenceBookIds | ForEach-Object { [string]$_ } | Sort-Object
) -join '|'

if (
    $onboardingStateShapeInvalid -or
    $onboardingInitial.data.status -ne 'PENDING' -or
    $null -ne $onboardingInitial.data.finishedAt -or
    @($onboardingInitial.data.preferredCategoryIds).Count -ne 0 -or
    @($onboardingInitial.data.referenceBookIds).Count -ne 0 -or
    $onboardingDraft.data.status -ne 'PENDING' -or
    $null -ne $onboardingDraft.data.finishedAt -or
    $draftPreferredCategorySignature -ne $expectedPreferredCategorySignature -or
    $draftReferenceBookSignature -ne $expectedReferenceBookSignature -or
    $onboardingCompleted.data.status -ne 'COMPLETED' -or
    [string]::IsNullOrWhiteSpace([string]$onboardingCompleted.data.finishedAt) -or
    $onboardingReadback.data.status -ne 'COMPLETED' -or
    $onboardingReadback.data.finishedAt -ne $onboardingCompleted.data.finishedAt -or
    $completedPreferredCategorySignature -ne $expectedPreferredCategorySignature -or
    $completedReferenceBookSignature -ne $expectedReferenceBookSignature
) {
    throw 'Onboarding draft, complete, readback hoặc response shape không hợp lệ.'
}

$completedAt = [DateTimeOffset]::Parse([string]$onboardingCompleted.data.finishedAt)
$skippedAt = [DateTimeOffset]::Parse([string]$onboardingSkipped.data.finishedAt)
if (
    $completedAt.Offset -ne [TimeSpan]::Zero -or
    $onboardingSkipped.data.status -ne 'SKIPPED' -or
    [string]::IsNullOrWhiteSpace([string]$onboardingSkipped.data.finishedAt) -or
    $skippedAt.Offset -ne [TimeSpan]::Zero -or
    $onboardingSkippedReadback.data.status -ne 'SKIPPED' -or
    $onboardingSkippedReadback.data.finishedAt -ne $onboardingSkipped.data.finishedAt -or
    @($onboardingSkippedReadback.data.preferredCategoryIds).Count -ne 0 -or
    @($onboardingSkippedReadback.data.referenceBookIds).Count -ne 0
) {
    throw 'Onboarding skip hoặc finishedAt UTC không hợp lệ.'
}

$onboardingRecommendationItems = @($onboardingRecommendations.data.items)
$onboardingRecommendedBookIds = @(
    $onboardingRecommendationItems |
        ForEach-Object { [string]$_.book.id }
)
$onboardingPreferenceReasons = @(
    $onboardingRecommendationItems |
        Where-Object { $_.reasonCode -in @('MATCHED_AUTHOR', 'MATCHED_CATEGORY') }
)
if (
    $onboardingRecommendationItems.Count -eq 0 -or
    @(
        $referenceBookIds |
            Where-Object { $_ -in $onboardingRecommendedBookIds }
    ).Count -gt 0 -or
    $onboardingPreferenceReasons.Count -eq 0
) {
    throw 'Recommendation không dùng preference hoặc còn trả lại reference book.'
}

$recommendationItems = @($recommendations.data.items)
$recommendationItemsRepeat = @($recommendationsRepeat.data.items)
$recommendationPageFields = @($recommendations.data.PSObject.Properties.Name)
$requiredRecommendationPageFields = @('items', 'page', 'pageSize', 'totalItems', 'totalPages')
$requiredRecommendationItemFields = @('book', 'reasonCode', 'reasonText')
$requiredRecommendationBookFields = @('id', 'title', 'shelf')
$recommendationReasonTexts = @{
    FOLLOWED_READER_LIKED = 'Được độc giả bạn theo dõi đánh giá cao.'
    MATCHED_AUTHOR = 'Cùng tác giả với sách bạn quan tâm.'
    MATCHED_CATEGORY = 'Cùng thể loại với sách bạn quan tâm.'
    POPULAR_FALLBACK = 'Được cộng đồng BookSpace đánh giá cao.'
}
$ownedBookIds = @($library.data.items | ForEach-Object { [string]$_.bookId })
$recommendationSmokeInvalid = (
    @(
        $requiredRecommendationPageFields |
            Where-Object { $_ -notin $recommendationPageFields }
    ).Count -gt 0 -or
    $recommendations.data.page -ne 1 -or
    $recommendations.data.pageSize -ne 12 -or
    $recommendations.data.totalItems -lt $recommendationItems.Count -or
    $recommendationItems.Count -eq 0
)

foreach ($recommendationItem in $recommendationItems) {
    $itemFields = @($recommendationItem.PSObject.Properties.Name)
    $bookFields = @($recommendationItem.book.PSObject.Properties.Name)
    $expectedReasonText = $recommendationReasonTexts[[string]$recommendationItem.reasonCode]
    if (
        @($requiredRecommendationItemFields | Where-Object { $_ -notin $itemFields }).Count -gt 0 -or
        @($itemFields | Where-Object { $_ -notin $requiredRecommendationItemFields }).Count -gt 0 -or
        @($requiredRecommendationBookFields | Where-Object { $_ -notin $bookFields }).Count -gt 0 -or
        -not $recommendationItem.book.id -or
        -not $recommendationItem.book.title -or
        $null -ne $recommendationItem.book.shelf -or
        [string]::IsNullOrWhiteSpace([string]$expectedReasonText) -or
        $recommendationItem.reasonText -ne $expectedReasonText -or
        [string]$recommendationItem.book.id -in $ownedBookIds
    ) {
        $recommendationSmokeInvalid = $true
        break
    }
}

$recommendationSignature = @(
    $recommendationItems |
        ForEach-Object { '{0}:{1}' -f $_.book.id, $_.reasonCode }
) -join '|'
$recommendationRepeatSignature = @(
    $recommendationItemsRepeat |
        ForEach-Object { '{0}:{1}' -f $_.book.id, $_.reasonCode }
) -join '|'
if (
    $recommendationItemsRepeat.Count -ne $recommendationItems.Count -or
    $recommendationRepeatSignature -ne $recommendationSignature
) {
    $recommendationSmokeInvalid = $true
}

if ($recommendationSmokeInvalid) {
    throw 'Recommendation PageResult, reason mapping, own-library exclusion hoặc ordering không hợp lệ.'
}

$adminRecommendationItems = @($adminRecommendations.data.items)
$adminRecommendationItemsRepeat = @($adminRecommendationsRepeat.data.items)
$adminRecommendationSignature = @(
    $adminRecommendationItems |
        ForEach-Object { '{0}:{1}' -f $_.book.id, $_.reasonCode }
) -join '|'
$adminRecommendationRepeatSignature = @(
    $adminRecommendationItemsRepeat |
        ForEach-Object { '{0}:{1}' -f $_.book.id, $_.reasonCode }
) -join '|'
if (
    $adminLibrary.data.totalItems -ne 0 -or
    $adminRecommendationItems.Count -eq 0 -or
    $adminRecommendations.data.page -ne 1 -or
    $adminRecommendations.data.pageSize -ne 12 -or
    @($adminRecommendationItems | Where-Object { $_.reasonCode -ne 'POPULAR_FALLBACK' }).Count -gt 0 -or
    $adminRecommendationItemsRepeat.Count -ne $adminRecommendationItems.Count -or
    $adminRecommendationRepeatSignature -ne $adminRecommendationSignature
) {
    throw 'Cold-start recommendation fallback hoặc ordering không hợp lệ.'
}

$haLinhSearch = @($people.data.items | Where-Object { $_.displayName -eq 'Hà Linh' })
$suggestionItems = @($peopleSuggestions.data.items)
$suggestionReasons = @(
    'MUTUAL_FOLLOWS',
    'FOLLOWS_YOU',
    'POPULAR_READER',
    'ACTIVE_READER',
    'NEW_READER'
)
$requiredPeopleFields = @(
    'id',
    'displayName',
    'bio',
    'avatarUrl',
    'followerCount',
    'booksReadCount',
    'isFollowing',
    'followsYou',
    'mutualFollowCount',
    'reason',
    'reasonText'
)
$searchFields = @($haLinhSearch[0].PSObject.Properties.Name)
$suggestionPageFields = @($peopleSuggestions.data.PSObject.Properties.Name)
$requiredSuggestionPageFields = @('items', 'page', 'pageSize', 'totalItems', 'totalPages')
$peopleSmokeInvalid = (
    $haLinhSearch.Count -ne 1 -or
    @($requiredPeopleFields | Where-Object { $_ -notin $searchFields }).Count -gt 0 -or
    $haLinhSearch[0].reason -ne 'SEARCH_MATCH' -or
    -not $haLinhSearch[0].id -or
    $null -eq $haLinhSearch[0].followerCount -or
    $null -eq $haLinhSearch[0].booksReadCount -or
    ($haLinhSearch[0].PSObject.Properties.Name -contains 'email') -or
    @(
        $requiredSuggestionPageFields |
            Where-Object { $_ -notin $suggestionPageFields }
    ).Count -gt 0 -or
    $peopleSuggestions.data.totalItems -lt $suggestionItems.Count
)
foreach ($suggestionItem in $suggestionItems) {
    $suggestionFields = @($suggestionItem.PSObject.Properties.Name)
    if (
        @($requiredPeopleFields | Where-Object { $_ -notin $suggestionFields }).Count -gt 0 -or
        $suggestionItem.reason -notin $suggestionReasons -or
        -not $suggestionItem.reasonText -or
        ($suggestionFields -contains 'email')
    ) {
        $peopleSmokeInvalid = $true
        break
    }
}
if ($peopleSmokeInvalid) {
    throw 'People Discovery public DTO, PageResult hoặc reason không hợp lệ.'
}

$feedItems = @($feed.data.items)
$feedTypes = @($feedItems | ForEach-Object { $_.type })
$feedPageFields = @($feed.data.PSObject.Properties.Name)
$requiredFeedPageFields = @('items', 'page', 'pageSize', 'totalItems', 'totalPages')
$feedSmokeInvalid = (
    @($requiredFeedPageFields | Where-Object { $_ -notin $feedPageFields }).Count -gt 0 -or
    $feed.data.page -ne 1 -or
    $feed.data.pageSize -ne 10 -or
    $feed.data.totalItems -lt $feedItems.Count -or
    $feedItems.Count -eq 0 -or
    'READING_PROGRESS' -notin $feedTypes -or
    'BOOK_FINISHED' -notin $feedTypes
)

foreach ($feedItem in $feedItems) {
    $feedItemFields = @($feedItem.PSObject.Properties.Name)
    if (
        $feedItem.type -notin @('READING_PROGRESS', 'BOOK_FINISHED') -or
        -not $feedItem.id -or
        -not $feedItem.createdAt -or
        $feedItemFields -contains 'note'
    ) {
        $feedSmokeInvalid = $true
        break
    }
}

for ($index = 1; $index -lt $feedItems.Count; $index++) {
    $previousItem = $feedItems[$index - 1]
    $currentItem = $feedItems[$index]
    $previousCreatedAt = [DateTimeOffset]::Parse([string]$previousItem.createdAt)
    $currentCreatedAt = [DateTimeOffset]::Parse([string]$currentItem.createdAt)

    if (
        $currentCreatedAt -gt $previousCreatedAt -or
        (
            $currentCreatedAt -eq $previousCreatedAt -and
            [string]::CompareOrdinal([string]$currentItem.id, [string]$previousItem.id) -gt 0
        )
    ) {
        $feedSmokeInvalid = $true
        break
    }
}

$serializedFeed = $feed.data | ConvertTo-Json -Depth 8 -Compress
foreach ($readingSession in @($readingSessions.data.items)) {
    if (
        -not [string]::IsNullOrWhiteSpace([string]$readingSession.note) -and
        $serializedFeed.Contains([string]$readingSession.note)
    ) {
        $feedSmokeInvalid = $true
        break
    }
}

if ($feedSmokeInvalid) {
    throw 'Feed filter, paging, ordering or note isolation is invalid.'
}

if ($null -ne $activeReadingSession.data) {
    $activeReadingFields = @($activeReadingSession.data.PSObject.Properties.Name)
    $requiredActiveReadingFields = @(
        'id',
        'bookId',
        'book',
        'status',
        'startPage',
        'startedAt',
        'elapsedSeconds',
        'updatedAt'
    )
    if (
        @($requiredActiveReadingFields | Where-Object { $_ -notin $activeReadingFields }).Count -gt 0 -or
        $activeReadingSession.data.status -notin @('RUNNING', 'PAUSED') -or
        [long]$activeReadingSession.data.elapsedSeconds -lt 0 -or
        $activeReadingFields -contains 'note' -or
        $activeReadingFields -contains 'userId' -or
        $activeReadingFields -contains 'accumulatedSeconds'
    ) {
        throw 'Focus Reading active DTO hoặc privacy contract không hợp lệ.'
    }
}

if (
    -not $moderationTarget.success -or
    -not $contentReport.success -or
    $contentReport.data.status -ne 'PENDING' -or
    @($moderationQueue.data.items | Where-Object { $_.id -eq $contentReport.data.id }).Count -ne 1 -or
    -not $moderationResolution.success -or
    $moderationResolution.data.status -ne 'DISMISSED' -or
    $moderationResolution.data.action -ne 'NONE'
) {
    throw 'Community Safety report, admin queue hoặc resolution contract không hợp lệ.'
}

$mutedSafetyEntry = @($safetyListMuted.data.items | Where-Object {
    $_.user.id -eq $moderationTarget.data.user.id
})
$blockedSafetyEntry = @($safetyListBlocked.data.items | Where-Object {
    $_.user.id -eq $moderationTarget.data.user.id
})
if (
    -not $safetyMute.success -or
    -not $safetyMute.data.isMuted -or
    $mutedSafetyEntry.Count -ne 1 -or
    -not $mutedSafetyEntry[0].isMuted -or
    -not $profileWhileMuted.success -or
    -not $profileWhileMuted.data.isMuted -or
    -not $safetyUnmute.success -or
    -not $safetyFollow.success -or
    -not $safetyFollow.data.isFollowing -or
    -not $safetyBlock.success -or
    -not $safetyBlock.data.isBlocked -or
    $safetyBlock.data.isMuted -or
    $profileWhileBlocked.StatusCode -ne 404 -or
    $profileWhileBlocked.Payload.code -ne 'USER_NOT_FOUND' -or
    $blockedSafetyEntry.Count -ne 1 -or
    -not $blockedSafetyEntry[0].isBlocked -or
    $blockedSafetyEntry[0].isMuted -or
    -not $safetyUnblock.success -or
    -not $profileAfterUnblock.success -or
    $profileAfterUnblock.data.isFollowing
) {
    throw 'Block/mute safety contract, visibility cloak hoặc follow cleanup không hợp lệ.'
}

if (
    $invalidFeedType.StatusCode -ne 400 -or
    $null -eq $invalidFeedType.Payload -or
    $invalidFeedType.Payload.success -ne $false -or
    $invalidFeedType.Payload.code -ne 'INVALID_FEED_TYPE' -or
    [string]::IsNullOrWhiteSpace([string]$invalidFeedType.Payload.message) -or
    -not [regex]::IsMatch([string]$invalidFeedType.Payload.message, '[^\u0000-\u007F]')
) {
    throw 'Invalid feed type did not return the localized 400 contract.'
}

if (
    $null -eq $externalCatalog -or
    -not $externalCatalog.success -or
    $null -eq $externalCatalog.data -or
    [string]::IsNullOrWhiteSpace([string]$externalCatalog.data.provider) -or
    [string]::IsNullOrWhiteSpace([string]$externalCatalog.data.message) -or
    $null -eq $externalCatalog.data.items
) {
    throw 'External catalog search không trả controlled provider contract.'
}

if (
    $null -eq $clubChatMessage -or
    -not $clubChatMessage.success -or
    $clubChatMessage.data.content -ne $chatContent -or
    ($clubChatMessage.data.sender.PSObject.Properties.Name -contains 'email') -or
    $null -eq $clubChatHistory -or
    -not $clubChatHistory.success -or
    @($clubChatHistory.data.items | Where-Object { $_.id -eq $clubChatMessage.data.id }).Count -ne 1 -or
    $null -eq $clubChatUnread -or
    -not $clubChatUnread.success -or
    $null -eq $clubChatReadState -or
    -not $clubChatReadState.success -or
    $clubChatReadState.data.lastReadMessageId -ne $clubChatMessage.data.id -or
    $clubChatReadState.data.count -ne 0
) {
    throw 'Club Chat persistence, public DTO, history hoặc read-state contract không hợp lệ.'
}

if (
    -not $directMessageReaderFollow.success -or
    -not $directMessageTargetFollow.success -or
    -not $directConversation.success -or
    -not $directMessage.success -or
    $directMessage.data.content -ne $directMessageContent -or
    ($directMessage.data.sender.PSObject.Properties.Name -contains 'email') -or
    -not $directMessageHistory.success -or
    @($directMessageHistory.data.items | Where-Object { $_.id -eq $directMessage.data.id }).Count -ne 1 -or
    -not $directMessageUnread.success -or
    $directMessageUnread.data.count -lt 1 -or
    -not $directMessageReadState.success -or
    $directMessageReadState.data.lastReadMessageId -ne $directMessage.data.id -or
    $directMessageReadState.data.count -ne 0
) {
    throw 'Direct Messages persistence, mutual follow, public DTO, unread hoặc read-state contract không hợp lệ.'
}

$bookListMineItem = @($bookListsMine.data.items | Where-Object { $_.id -eq $bookList.data.id })
if (
    $privateBookList.StatusCode -ne 404 -or
    $privateBookList.Payload.code -ne 'BOOK_LIST_NOT_FOUND' -or
    $publicBookList.data.visibility -ne 'PUBLIC' -or
    $publicBookList.data.items.Count -ne 1 -or
    $publicBookList.data.items[0].book.id -ne $books.data.items[0].id -or
    $bookListReorder.data.items[0].position -ne 0 -or
    $bookListMineItem.Count -ne 1 -or
    -not $bookListMineItem[0].containsBook -or
    $deletedBookList.StatusCode -ne 404 -or
    $deletedBookList.Payload.code -ne 'BOOK_LIST_NOT_FOUND'
) {
    throw 'Book Lists create/privacy/add/reorder/contains/public/delete contract không hợp lệ.'
}

if (
    $insightsCalendar.data.daysData.Count -ne 365 -or
    $insightsWeekly.data.items.Count -ne 12 -or
    $insightsMonthly.data.items.Count -ne 12
) {
    throw "Reading Insights không trả đủ các bucket lịch, tuần hoặc tháng."
}

[pscustomobject]@{
    Health = $health
    CorrelationId = $returnedHealthCorrelationId
    User = $login.data.user.email
    PeopleSearchResults = $people.data.totalItems
    PeopleSuggestions = $peopleSuggestions.data.totalItems
    Onboarding = $onboardingReadback.data.status
    SkippedOnboarding = $onboardingSkippedReadback.data.status
    OnboardingRecommendations = $onboardingRecommendations.data.totalItems
    Recommendations = $recommendations.data.totalItems
    ColdStartRecommendations = $adminRecommendations.data.totalItems
    ExternalCatalogAvailable = [bool]$externalCatalog.data.available
    ReadingFeedItems = $feed.data.totalItems
    Books = $books.data.totalItems
    LibraryItems = $library.data.totalItems
    ReadingSessions = $readingSessions.data.totalItems
    FocusSessionState = if ($null -ne $activeReadingSession.data) { $activeReadingSession.data.status } else { 'NONE' }
    BooksRead = $dashboard.data.booksRead
    ReadingGoals = $goals.data.totalItems
    ReadingNotes = $notes.data.totalItems
    Clubs = $clubs.data.totalItems
    ClubInvitations = $clubInvitations.data.totalItems
    FirstClubMembers = if ($null -ne $clubMembers) { $clubMembers.data.totalItems } else { 0 }
    FirstClubReadingSprints = if ($null -ne $clubSprints) { $clubSprints.data.totalItems } else { 0 }
    FirstSprintParticipants = if ($null -ne $sprintLeaderboard) { $sprintLeaderboard.data.totalItems } else { 0 }
    FirstSprintTimelineItems = if ($null -ne $sprintTimeline) { $sprintTimeline.data.totalItems } else { 0 }
    ClubChatMessages = if ($null -ne $clubChatHistory) { $clubChatHistory.data.items.Count } else { 0 }
    DirectMessages = if ($null -ne $directMessageHistory) { $directMessageHistory.data.items.Count } else { 0 }
    BookLists = 'PASS'
    ModerationReports = $moderationQueue.data.totalItems
    SafetyControls = 'PASS'
    CurrentStreak = $insightsOverview.data.currentStreak
    InsightCalendarDays = $insightsCalendar.data.daysData.Count
    InsightWeeks = $insightsWeekly.data.items.Count
    InsightMonths = $insightsMonthly.data.items.Count
    Status = 'PASS'
}
