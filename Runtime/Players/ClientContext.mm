#import <Foundation/Foundation.h>

// IL2CPP frees the returned string.
extern "C" const char* ToabaPreferredLanguage()
{
    NSString* language = NSLocale.preferredLanguages.firstObject;
    return language ? strdup(language.UTF8String) : NULL;
}
