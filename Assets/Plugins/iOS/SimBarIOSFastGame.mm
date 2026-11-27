#import <UIKit/UIKit.h>
#import <WebKit/WebKit.h>

static NSURL *SimBarStartURL(void)
{
    return [NSURL URLWithString:@"https://jtsgrit0.github.io/simbar_63/"];
}

static BOOL SimBarAllowsPage(NSURL *url)
{
    if (!url || ![url.scheme.lowercaseString isEqualToString:@"https"]
        || ![url.host.lowercaseString isEqualToString:@"jtsgrit0.github.io"]
        || url.user || url.password || url.port) return NO;
    NSString *path = url.path ?: @"";
    return [path isEqualToString:@"/simbar_63"]
        || [path isEqualToString:@"/simbar_63/"]
        || [path isEqualToString:@"/simbar_63/index.html"];
}

@interface SimBarIOSFastGameView : UIView <WKNavigationDelegate>
@property(nonatomic, strong) WKWebView *web;
@property(nonatomic, strong) UIStackView *errorPanel;
@property(nonatomic, strong) UILabel *errorLabel;
- (void)retry;
@end

@implementation SimBarIOSFastGameView

- (instancetype)init
{
    self = [super initWithFrame:CGRectZero];
    if (!self) return nil;
    self.backgroundColor = [UIColor colorWithRed:0.039 green:0.055 blue:0.075 alpha:1];

    WKWebViewConfiguration *configuration = [[WKWebViewConfiguration alloc] init];
    configuration.websiteDataStore = [WKWebsiteDataStore defaultDataStore];
    configuration.allowsInlineMediaPlayback = YES;
    configuration.mediaTypesRequiringUserActionForPlayback = WKAudiovisualMediaTypeAll;
    self.web = [[WKWebView alloc] initWithFrame:CGRectZero configuration:configuration];
    self.web.translatesAutoresizingMaskIntoConstraints = NO;
    self.web.navigationDelegate = self;
    self.web.opaque = NO;
    self.web.backgroundColor = self.backgroundColor;
    self.web.scrollView.contentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentNever;
    self.web.scrollView.bounces = NO;
    [self addSubview:self.web];
    UILayoutGuide *safe = self.safeAreaLayoutGuide;
    [NSLayoutConstraint activateConstraints:@[
        [self.web.leadingAnchor constraintEqualToAnchor:safe.leadingAnchor],
        [self.web.trailingAnchor constraintEqualToAnchor:safe.trailingAnchor],
        [self.web.topAnchor constraintEqualToAnchor:safe.topAnchor],
        [self.web.bottomAnchor constraintEqualToAnchor:safe.bottomAnchor]
    ]];

    self.errorLabel = [[UILabel alloc] init];
    self.errorLabel.textColor = UIColor.whiteColor;
    self.errorLabel.textAlignment = NSTextAlignmentCenter;
    self.errorLabel.numberOfLines = 0;
    UIButton *retry = [UIButton buttonWithType:UIButtonTypeSystem];
    [retry setTitle:@"Retry" forState:UIControlStateNormal];
    retry.tintColor = UIColor.whiteColor;
    [retry addTarget:self action:@selector(retry) forControlEvents:UIControlEventTouchUpInside];
    self.errorPanel = [[UIStackView alloc] initWithArrangedSubviews:@[self.errorLabel,retry]];
    self.errorPanel.translatesAutoresizingMaskIntoConstraints = NO;
    self.errorPanel.axis = UILayoutConstraintAxisVertical;
    self.errorPanel.alignment = UIStackViewAlignmentCenter;
    self.errorPanel.spacing = 18;
    [self addSubview:self.errorPanel];
    [NSLayoutConstraint activateConstraints:@[
        [self.errorPanel.centerXAnchor constraintEqualToAnchor:safe.centerXAnchor],
        [self.errorPanel.centerYAnchor constraintEqualToAnchor:safe.centerYAnchor],
        [self.errorPanel.widthAnchor constraintLessThanOrEqualToAnchor:safe.widthAnchor constant:-32]
    ]];
    [self retry];
    return self;
}

- (void)retry
{
    self.errorPanel.hidden = YES;
    self.web.hidden = NO;
    NSURLRequest *request = [NSURLRequest requestWithURL:SimBarStartURL()
        cachePolicy:NSURLRequestReloadIgnoringLocalCacheData timeoutInterval:30];
    [self.web loadRequest:request];
}

- (void)showLoadError
{
    self.web.hidden = YES;
    self.errorLabel.text = @"SIMBAR could not load. Check your internet connection and try again.";
    self.errorPanel.hidden = NO;
}

- (void)webView:(WKWebView *)webView decidePolicyForNavigationAction:(WKNavigationAction *)action
    decisionHandler:(void (^)(WKNavigationActionPolicy))decisionHandler
{
    NSURL *url = action.request.URL;
    if (SimBarAllowsPage(url) && action.targetFrame && action.targetFrame.isMainFrame)
    {
        decisionHandler(WKNavigationActionPolicyAllow);
        return;
    }
    if (action.navigationType == WKNavigationTypeLinkActivated && action.sourceFrame.isMainFrame
        && [url.scheme.lowercaseString isEqualToString:@"https"] && url.host.length && !url.user && !url.password
        && !SimBarAllowsPage(url))
    {
        [[UIApplication sharedApplication] openURL:url options:@{} completionHandler:nil];
    }
    decisionHandler(WKNavigationActionPolicyCancel);
}

- (void)webView:(WKWebView *)webView decidePolicyForNavigationResponse:(WKNavigationResponse *)response
    decisionHandler:(void (^)(WKNavigationResponsePolicy))decisionHandler
{
    NSHTTPURLResponse *http = [response.response isKindOfClass:NSHTTPURLResponse.class]
        ? (NSHTTPURLResponse *)response.response : nil;
    if (response.isForMainFrame && (!SimBarAllowsPage(response.response.URL) || (http && http.statusCode >= 400)))
    {
        [self showLoadError];
        decisionHandler(WKNavigationResponsePolicyCancel);
        return;
    }
    decisionHandler(WKNavigationResponsePolicyAllow);
}

- (void)webView:(WKWebView *)webView didFailProvisionalNavigation:(WKNavigation *)navigation withError:(NSError *)error
{
    if (!([error.domain isEqualToString:NSURLErrorDomain] && error.code == NSURLErrorCancelled))
        [self showLoadError];
}

- (void)webView:(WKWebView *)webView didFailNavigation:(WKNavigation *)navigation withError:(NSError *)error
{
    if (!([error.domain isEqualToString:NSURLErrorDomain] && error.code == NSURLErrorCancelled))
        [self showLoadError];
}

- (void)webViewWebContentProcessDidTerminate:(WKWebView *)webView
{
    [self showLoadError];
}

@end

static SimBarIOSFastGameView *simBarGame;
static BOOL simBarInstalling;

static UIWindow *SimBarForegroundWindow(void)
{
    UIWindow *fallback = nil;
    for (UIScene *scene in UIApplication.sharedApplication.connectedScenes)
    {
        if (![scene isKindOfClass:UIWindowScene.class] || scene.activationState == UISceneActivationStateBackground
            || scene.activationState == UISceneActivationStateUnattached) continue;
        for (UIWindow *window in ((UIWindowScene *)scene).windows)
        {
            if (window.isKeyWindow && window.rootViewController) return window;
            if (!fallback && !window.isHidden && window.windowLevel == UIWindowLevelNormal && window.rootViewController)
                fallback = window;
        }
    }
    if (fallback) return fallback;
    id<UIApplicationDelegate> delegate = UIApplication.sharedApplication.delegate;
    return [delegate respondsToSelector:@selector(window)] ? delegate.window : nil;
}

static void SimBarAttachGame(NSUInteger attempt)
{
    UIWindow *window = SimBarForegroundWindow();
    UIView *root = window.rootViewController.view;
    if (!root)
    {
        if (attempt < 120)
            dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.1 * NSEC_PER_SEC)),
                dispatch_get_main_queue(), ^{ SimBarAttachGame(attempt + 1); });
        else
        {
            simBarInstalling = NO;
            NSLog(@"SIMBAR: No Unity foreground window for FastGame.");
        }
        return;
    }
    simBarGame = [[SimBarIOSFastGameView alloc] init];
    simBarGame.translatesAutoresizingMaskIntoConstraints = NO;
    [root addSubview:simBarGame];
    [NSLayoutConstraint activateConstraints:@[
        [simBarGame.leadingAnchor constraintEqualToAnchor:root.leadingAnchor],
        [simBarGame.trailingAnchor constraintEqualToAnchor:root.trailingAnchor],
        [simBarGame.topAnchor constraintEqualToAnchor:root.topAnchor],
        [simBarGame.bottomAnchor constraintEqualToAnchor:root.bottomAnchor]
    ]];
    simBarInstalling = NO;
}

extern "C" void SimBarIOSOpenFastGame(void)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (simBarGame || simBarInstalling) return;
        simBarInstalling = YES;
        SimBarAttachGame(0);
    });
}
