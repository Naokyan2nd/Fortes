// Assets/Plugins/iOS/IOSLocationPlugin.mm
#import <Foundation/Foundation.h>
#import <CoreLocation/CoreLocation.h>
#import <math.h>
#import <string.h>
#import <UIKit/UIKit.h>

// Must match LocationManager.cs background distance gate.
static const double kBackgroundMinDeltaMeters = 10.0;
static const double kBackgroundAccuracyMultiplier = 1.5;
static const double kBackgroundMaxAccuracyForMultiplier = 65.0;
static const double kBackgroundRejectAccuracyMeters = 100.0;
static const NSTimeInterval kMaxLocationAgeSeconds = 60.0;

static NSString *g_prefsPrefix = @"";
static BOOL g_distanceAccumulationEnabled = YES;
static BOOL g_useBackgroundAccumulatedBucket = NO;

// IL2CPP marshals returned strings with free(); must return malloc'd copies.
static char *CopyCStringForManaged(NSString *value) {
    if (value == nil || value.length == 0) {
        char *empty = (char *)malloc(1);
        if (empty != NULL) {
            empty[0] = '\0';
        }
        return empty;
    }

    const char *utf8 = [value UTF8String];
    if (utf8 == NULL) {
        char *empty = (char *)malloc(1);
        if (empty != NULL) {
            empty[0] = '\0';
        }
        return empty;
    }

    size_t length = strlen(utf8);
    char *result = (char *)malloc(length + 1);
    if (result == NULL) {
        return NULL;
    }

    memcpy(result, utf8, length + 1);
    return result;
}

static NSString *PrefsKey(NSString *shortKey) {
    if (g_prefsPrefix.length == 0) {
        return shortKey;
    }
    return [g_prefsPrefix stringByAppendingString:shortKey];
}

static double HaversineMeters(double lat1, double lon1, double lat2, double lon2) {
    const double R = 6371000.0;
    double phi1 = lat1 * M_PI / 180.0;
    double phi2 = lat2 * M_PI / 180.0;
    double dPhi = (lat2 - lat1) * M_PI / 180.0;
    double dLambda = (lon2 - lon1) * M_PI / 180.0;
    double a = sin(dPhi / 2.0) * sin(dPhi / 2.0) +
               cos(phi1) * cos(phi2) * sin(dLambda / 2.0) * sin(dLambda / 2.0);
    double c = 2.0 * atan2(sqrt(a), sqrt(1.0 - a));
    return R * c;
}

static double BackgroundMinDeltaMeters(CLLocation *location) {
    double minDelta = kBackgroundMinDeltaMeters;
    if (location.horizontalAccuracy > 0.0 &&
        location.horizontalAccuracy <= kBackgroundMaxAccuracyForMultiplier) {
        minDelta = fmax(minDelta, location.horizontalAccuracy * kBackgroundAccuracyMultiplier);
    }
    return minDelta;
}

static BOOL IsUsableBackgroundLocation(CLLocation *location) {
    if (location == nil) {
        return NO;
    }

    if (location.horizontalAccuracy <= 0.0 ||
        location.horizontalAccuracy > kBackgroundRejectAccuracyMeters) {
        return NO;
    }

    NSTimeInterval age = -[location.timestamp timeIntervalSinceNow];
    if (age > kMaxLocationAgeSeconds) {
        return NO;
    }

    double lat = location.coordinate.latitude;
    double lng = location.coordinate.longitude;
    if (fabs(lat) < 0.0001 && fabs(lng) < 0.0001) {
        return NO;
    }

    return fabs(lat) <= 90.0 && fabs(lng) <= 180.0;
}

static NSString *DocumentsLogPath(void) {
    NSArray *paths = NSSearchPathForDirectoriesInDomains(NSDocumentDirectory, NSUserDomainMask, YES);
    NSString *documentsDirectory = [paths firstObject];
    return [documentsDirectory stringByAppendingPathComponent:@"positions_log.txt"];
}

static void AppendPositionLog(NSString *tag, double lat, double lng) {
    NSString *filePath = DocumentsLogPath();
    NSDateFormatter *formatter = [[NSDateFormatter alloc] init];
    formatter.locale = [NSLocale localeWithLocaleIdentifier:@"en_US_POSIX"];
    [formatter setDateFormat:@"yyyy-MM-dd HH:mm:ss"];
    NSString *dateString = [formatter stringFromDate:[NSDate date]];

    NSString *logLine = [NSString stringWithFormat:@"%@,%@,%.8f,%.8f\n",
                         dateString, tag, lat, lng];

    NSFileHandle *fileHandle = [NSFileHandle fileHandleForWritingAtPath:filePath];
    if (fileHandle) {
        [fileHandle seekToEndOfFile];
        [fileHandle writeData:[logLine dataUsingEncoding:NSUTF8StringEncoding]];
        [fileHandle closeFile];
    } else {
        [logLine writeToFile:filePath atomically:YES encoding:NSUTF8StringEncoding error:nil];
    }
}

@interface IOSLocationTracker : NSObject <CLLocationManagerDelegate>
@property (nonatomic, strong) CLLocationManager *significantLocationManager;
@property (nonatomic, strong) CLLocationManager *continuousLocationManager;
@property (nonatomic, assign) BOOL isMonitoringSignificantChanges;
@property (nonatomic, assign) BOOL isBackgroundDeferredActive;
@property (nonatomic, assign) BOOL hasRequestedAuthorization;
@property (nonatomic, assign) CLAuthorizationStatus cachedAuthorizationStatus;
@property (nonatomic, copy) NSString *cachedAuthorizationLabel;
+ (instancetype)sharedInstance;
- (void)tryStartSignificantMonitoring;
- (void)startBackgroundDeferredLocation;
- (void)stopBackgroundDeferredLocation;
@end

@implementation IOSLocationTracker

+ (instancetype)sharedInstance {
    static IOSLocationTracker *instance = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        instance = [[IOSLocationTracker alloc] init];
    });
    return instance;
}

- (instancetype)init {
    self = [super init];
    if (self) {
        _isMonitoringSignificantChanges = NO;
        _isBackgroundDeferredActive = NO;
        _hasRequestedAuthorization = NO;
        _cachedAuthorizationStatus = kCLAuthorizationStatusNotDetermined;
        _cachedAuthorizationLabel = @"Not Determined";

        self.significantLocationManager = [[CLLocationManager alloc] init];
        self.significantLocationManager.delegate = self;
        self.significantLocationManager.desiredAccuracy = kCLLocationAccuracyHundredMeters;
        self.significantLocationManager.pausesLocationUpdatesAutomatically = NO;
        self.significantLocationManager.activityType = CLActivityTypeFitness;

        self.continuousLocationManager = [[CLLocationManager alloc] init];
        self.continuousLocationManager.delegate = self;
        self.continuousLocationManager.desiredAccuracy = kCLLocationAccuracyHundredMeters;
        self.continuousLocationManager.pausesLocationUpdatesAutomatically = NO;
        self.continuousLocationManager.activityType = CLActivityTypeFitness;
        self.continuousLocationManager.distanceFilter = 25.0;
    }
    return self;
}

- (NSString *)authorizationLabelForStatus:(CLAuthorizationStatus)status {
    switch (status) {
        case kCLAuthorizationStatusAuthorizedAlways:
            return @"Always";
        case kCLAuthorizationStatusAuthorizedWhenInUse:
            return @"When In Use";
        case kCLAuthorizationStatusDenied:
            return @"Denied";
        case kCLAuthorizationStatusRestricted:
            return @"Restricted";
        case kCLAuthorizationStatusNotDetermined:
        default:
            return @"Not Determined";
    }
}

- (void)updateCachedAuthorizationFromManager:(CLLocationManager *)manager {
    CLAuthorizationStatus status = manager.authorizationStatus;
    self.cachedAuthorizationStatus = status;
    self.cachedAuthorizationLabel = [self authorizationLabelForStatus:status];
}

- (BOOL)hasAlwaysAuthorization {
    return self.cachedAuthorizationStatus == kCLAuthorizationStatusAuthorizedAlways;
}

- (NSString *)authorizationLabel {
    return self.cachedAuthorizationLabel ?: @"Not Determined";
}

- (void)tryStartSignificantMonitoring {
    if (self.isMonitoringSignificantChanges) {
        return;
    }

    if (![CLLocationManager significantLocationChangeMonitoringAvailable]) {
        NSLog(@"IOSLocationPlugin: significant location change monitoring is not available.");
        return;
    }

    if (self.cachedAuthorizationStatus == kCLAuthorizationStatusNotDetermined ||
        self.cachedAuthorizationStatus == kCLAuthorizationStatusAuthorizedWhenInUse) {
        // Wait for locationManagerDidChangeAuthorization; do not request/check on this call path.
        return;
    }

    if (![self hasAlwaysAuthorization]) {
        NSLog(@"IOSLocationPlugin: Always authorization required for background SLC.");
        return;
    }

    [self.significantLocationManager startMonitoringSignificantLocationChanges];
    self.isMonitoringSignificantChanges = YES;
    NSLog(@"IOSLocationPlugin: significant location monitoring started (separate manager).");
}

- (void)stopSignificantMonitoring {
    if (!self.isMonitoringSignificantChanges) {
        return;
    }

    [self.significantLocationManager stopMonitoringSignificantLocationChanges];
    self.isMonitoringSignificantChanges = NO;
}

- (void)startBackgroundDeferredLocation {
    if (self.isBackgroundDeferredActive) {
        return;
    }

    if (![self hasAlwaysAuthorization]) {
        NSLog(@"IOSLocationPlugin: Always authorization required for deferred background location.");
        [self tryStartSignificantMonitoring];
        return;
    }

    [self tryStartSignificantMonitoring];

    self.continuousLocationManager.allowsBackgroundLocationUpdates = YES;

    if ([CLLocationManager deferredLocationUpdatesAvailable]) {
        [self.continuousLocationManager allowDeferredLocationUpdatesUntilTraveled:50.0
                                                                        timeout:300.0];
    }

    [self.continuousLocationManager startUpdatingLocation];
    self.isBackgroundDeferredActive = YES;
    NSLog(@"IOSLocationPlugin: continuous background location started (SLC on separate manager).");
}

- (void)stopBackgroundDeferredLocation {
    if (!self.isBackgroundDeferredActive) {
        return;
    }

    if ([CLLocationManager deferredLocationUpdatesAvailable]) {
        [self.continuousLocationManager disallowDeferredLocationUpdates];
    }

    [self.continuousLocationManager stopUpdatingLocation];
    self.continuousLocationManager.allowsBackgroundLocationUpdates = NO;
    self.isBackgroundDeferredActive = NO;
    NSLog(@"IOSLocationPlugin: continuous background location stopped.");
}

- (void)processLocation:(CLLocation *)location
                    tag:(NSString *)tag
            incrementMmCount:(BOOL)incrementMmCount {
    if (!IsUsableBackgroundLocation(location)) {
        return;
    }

    double lat = location.coordinate.latitude;
    double lng = location.coordinate.longitude;
    NSUserDefaults *prefs = [NSUserDefaults standardUserDefaults];

    if (incrementMmCount) {
        NSString *mmCountKey = PrefsKey(@"MM_ActivatedCount");
        NSInteger mmCount = [prefs integerForKey:mmCountKey] + 1;
        [prefs setInteger:mmCount forKey:mmCountKey];
    }

    NSString *latKey = PrefsKey(@"LastSavedLat");
    NSString *lngKey = PrefsKey(@"LastSavedLng");
    NSString *distanceKey = PrefsKey(@"TotalDistance");
    NSString *backgroundKey = PrefsKey(@"OutGame_BackgroundAccumulatedMeters");

    if (g_distanceAccumulationEnabled && [prefs objectForKey:latKey] != nil) {
        double savedLat = [prefs floatForKey:latKey];
        double savedLng = [prefs floatForKey:lngKey];
        double delta = HaversineMeters(savedLat, savedLng, lat, lng);
        double minDelta = BackgroundMinDeltaMeters(location);
        if (delta > minDelta) {
            if (g_useBackgroundAccumulatedBucket) {
                double backgroundMeters = [prefs floatForKey:backgroundKey];
                backgroundMeters += delta;
                [prefs setFloat:(float)backgroundMeters forKey:backgroundKey];
            } else {
                double totalDistance = [prefs floatForKey:distanceKey];
                totalDistance += delta;
                [prefs setFloat:(float)totalDistance forKey:distanceKey];
            }
        }
    }

    [prefs setFloat:(float)lat forKey:latKey];
    [prefs setFloat:(float)lng forKey:lngKey];
    [prefs synchronize];

    AppendPositionLog(tag, lat, lng);
}

- (void)locationManagerDidChangeAuthorization:(CLLocationManager *)manager {
    [self updateCachedAuthorizationFromManager:manager];
    CLAuthorizationStatus status = self.cachedAuthorizationStatus;

    if (status == kCLAuthorizationStatusNotDetermined) {
        if (!self.hasRequestedAuthorization) {
            self.hasRequestedAuthorization = YES;
            [manager requestAlwaysAuthorization];
        }
        return;
    }

    if (status == kCLAuthorizationStatusAuthorizedWhenInUse) {
        if (!self.hasRequestedAuthorization) {
            self.hasRequestedAuthorization = YES;
            [manager requestAlwaysAuthorization];
        }
        return;
    }

    if (status == kCLAuthorizationStatusAuthorizedAlways) {
        self.hasRequestedAuthorization = NO;
        [self tryStartSignificantMonitoring];
        return;
    }

    if (status == kCLAuthorizationStatusDenied ||
        status == kCLAuthorizationStatusRestricted) {
        self.hasRequestedAuthorization = NO;
        [self stopSignificantMonitoring];
        [self stopBackgroundDeferredLocation];
    }
}

- (void)locationManager:(CLLocationManager *)manager didChangeAuthorizationStatus:(CLAuthorizationStatus)status {
    [self locationManagerDidChangeAuthorization:manager];
}

- (void)locationManager:(CLLocationManager *)manager didFailWithError:(NSError *)error {
    NSLog(@"IOSLocationPlugin location error: %@", error.localizedDescription);
}

- (void)locationManager:(CLLocationManager *)manager didUpdateLocations:(NSArray<CLLocation *> *)locations {
    CLLocation *location = [locations lastObject];
    if (!location) {
        return;
    }

    if (manager == self.continuousLocationManager) {
        if (!self.isBackgroundDeferredActive) {
            return;
        }

        [self processLocation:location
                            tag:@"iOS_Background_Deferred"
                 incrementMmCount:NO];
        return;
    }

    if (manager == self.significantLocationManager) {
        [self processLocation:location
                            tag:@"iOS_Background_Significant"
                 incrementMmCount:YES];
    }
}

- (void)locationManager:(CLLocationManager *)manager didFinishDeferredUpdatesWithError:(NSError *)error {
    if (manager != self.continuousLocationManager) {
        return;
    }

    if (error != nil) {
        NSLog(@"IOSLocationPlugin deferred updates finished with error: %@", error.localizedDescription);
    }

    if (!self.isBackgroundDeferredActive) {
        return;
    }

    if ([CLLocationManager deferredLocationUpdatesAvailable]) {
        [manager allowDeferredLocationUpdatesUntilTraveled:50.0 timeout:300.0];
    }
}

@end

#define IOS_LOCATION_PLUGIN_EXPORT extern "C" __attribute__((visibility("default")))

IOS_LOCATION_PLUGIN_EXPORT void _SetIOSLocationDistanceAccumulationEnabled(bool enabled) {
    g_distanceAccumulationEnabled = enabled ? YES : NO;
}

IOS_LOCATION_PLUGIN_EXPORT void _SetIOSLocationUseBackgroundAccumulatedBucket(bool enabled) {
    g_useBackgroundAccumulatedBucket = enabled ? YES : NO;
}

IOS_LOCATION_PLUGIN_EXPORT void _ConfigureIOSLocationPlugin(const char *companyName, const char *productName) {
    if (companyName == NULL || productName == NULL) {
        g_prefsPrefix = @"";
        return;
    }
    g_prefsPrefix = [NSString stringWithFormat:@"%s.%s.", companyName, productName];
}

IOS_LOCATION_PLUGIN_EXPORT void _StartSignificantLocationTracking(void) {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[IOSLocationTracker sharedInstance] tryStartSignificantMonitoring];
    });
}

IOS_LOCATION_PLUGIN_EXPORT void _StartBackgroundDeferredLocation(void) {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[IOSLocationTracker sharedInstance] startBackgroundDeferredLocation];
    });
}

IOS_LOCATION_PLUGIN_EXPORT void _StopBackgroundDeferredLocation(void) {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[IOSLocationTracker sharedInstance] stopBackgroundDeferredLocation];
    });
}

IOS_LOCATION_PLUGIN_EXPORT char *_GetIOSDocumentsPath(void) {
    NSArray *paths = NSSearchPathForDirectoriesInDomains(NSDocumentDirectory, NSUserDomainMask, YES);
    NSString *documentsDirectory = [paths firstObject];
    return CopyCStringForManaged(documentsDirectory);
}

IOS_LOCATION_PLUGIN_EXPORT char *_GetIOSLocationAuthorizationLabel(void) {
    NSString *label = [[IOSLocationTracker sharedInstance] authorizationLabel];
    return CopyCStringForManaged(label);
}
