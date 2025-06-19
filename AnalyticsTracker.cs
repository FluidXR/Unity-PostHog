using System;
using System.Collections.Generic;
using UnityEngine;
using PostHog;
using PostHog.Model;
using UnityPosthog.Utilities;

namespace UnityPosthog.Analytics
{
    public static class AnalyticsTracker
    {
        public static bool developer = false;
        public static DateTime SessionStartTime;
        
        public readonly static IPostHogClient posthogClient = new PostHogClient("<YOUR POSTHOG TOKEN>"); // NEVER EVER USE THIS DIRECTLY, ALWAYS USE THE TRACK FUNCTION!!!
        public static string UserID;
        public static bool initialized = false;

        public static void SetSessionStartTime(DateTime startTime)
        {
            SessionStartTime = startTime;
        }

        public static void Track(string eventName, Properties? properties = null, DateTime? timestamp = null)
        {
            // add session time to properties by creating new dictionary
            float session_time = (float)(DateTime.Now - SessionStartTime).TotalSeconds;
            Dictionary<string, object> newProperties = new Dictionary<string, object>
            {
                { "session_time", session_time.ToString() }
            };
            if (properties != null)
            {
                foreach (KeyValuePair<string, object> entry in properties)
                {
                    newProperties.Add(entry.Key, entry.Value);
                }
            }
            properties = AnalyticsTracker.makeProperties(newProperties);
            posthogClient.Capture(UserID, eventName, properties, timestamp);
        }

        public static void CheckAndSetUserInfo()
        {
            if (initialized)
            {
                return;
            }
            string HashedDeviceId = IdentifierHasher.HashIdentifier(SystemInfo.deviceUniqueIdentifier);


            // check playerprefs for user id, if none exists, generate one and save it
            if (PlayerPrefs.HasKey("UserID"))
            {
                UserID = PlayerPrefs.GetString("UserID");
                // You can modify the properties here that get associated with each user. When they log in, these properties will be updated on their profile in Posthog
                Identify(UserID, makeProperties(null, null, new Dictionary<string, object> { {"user_id", UserID}, {"device_id", HashedDeviceId}}));
                Track("app_opened");
            }
            else
            {
                // if it is the first time opening the app
                UserID = Guid.NewGuid().ToString();
                PlayerPrefs.SetString("UserID", UserID);
                Identify(UserID, makeProperties(null, null, new Dictionary<string, object> { {"user_id", UserID}, {"device_id", HashedDeviceId}}));
                Track("app_first_opened");
                Track("app_opened");

            }
            initialized = true;
        }

        public static void Identify(string distinctId, Properties? properties = null, DateTime? timestamp = null)
        {
            UserID = distinctId;
            posthogClient.Identify(distinctId, properties, timestamp);
        }

        public static Properties makeProperties(Dictionary<string, object> eventProperties,
                Dictionary<string, object> userPropertiesToSet,
                Dictionary<string, object> userPropertiesToSetOnce)
        {
            if (userPropertiesToSet == null)
                userPropertiesToSet = new Dictionary<string, object>();
            if (userPropertiesToSetOnce == null)
                userPropertiesToSetOnce = new Dictionary<string, object>();

            Properties properties = new Properties(
                eventProperties??new Dictionary<string, object>()
                );
            foreach (KeyValuePair<string, object> pair in userPropertiesToSet)
            {
                properties.SetUserProperty(pair.Key, pair.Value);
            }
            foreach (KeyValuePair<string, object> pair in userPropertiesToSetOnce)
            {
                properties.SetUserPropertyOnce(pair.Key, pair.Value);
            }
            return properties;
        }

        public static Properties makeProperties(Dictionary<string, object> eventProperties)
        {
            return new Properties(
                eventProperties??new Dictionary<string, object>()
                );
        }
    }
}
