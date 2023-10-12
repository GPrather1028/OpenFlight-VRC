﻿
using System;
using OpenFlightVRC.Net;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

// future random idea im storing here
// if you are a openflight contributer, use rainbow particles instead of the default
// allow them to be disabled in the settings
namespace OpenFlightVRC.Effects
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class EffectsHandler : UdonSharpBehaviour
    {
        public PlayerInfoStore playerInfoStore;

        [Header("VFX")]
        public bool VFX = true;
        public ParticleSystem LeftWingTrail;
        public ParticleSystem RightWingTrail;

        [Header("Sounds")]
        public bool SFX = true;
        public AudioSource FlapSound;

        public AudioSource GlideSound;
        public float minGlidePitch = 0.5f;
        public float maxGlidePitch = 1.5f;
        public float minGlideVelocity = 5f;
        public float maxGlideVelocity = 20f;

        //Previous Frame Data
        private bool wasGliding = false;
        private bool wasFlapping = false;

        private ParticleSystem.MinMaxGradient gradient;
        void Start()
        {
            //generate a rainbow gradient
            Gradient rainbowGradient = new Gradient();
            //make the gradient loop nicely
            rainbowGradient.SetKeys(
                GenerateRainbow(),
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f) }
            );

            gradient = new ParticleSystem.MinMaxGradient(rainbowGradient);
            gradient.mode = ParticleSystemGradientMode.Gradient;
        }

        private GradientColorKey[] GenerateRainbow()
        {
            //make sure the gradient loops back to the starting color
            GradientColorKey[] rainbow = new GradientColorKey[8];

            for (int i = 0; i < 8; i++)
            {
                var color = Color.HSVToRGB(i / 7f, 1, 1);
                rainbow[i] = new GradientColorKey(color, i / 7f);
            }

            return rainbow;
        }

        public void OwnerChanged()
        {
            //set the owner of the trail objects
            Networking.SetOwner(playerInfoStore.Owner, LeftWingTrail.gameObject);
            Networking.SetOwner(playerInfoStore.Owner, RightWingTrail.gameObject);
        }

        //TODO: Make this not so fucking horrible. This organizationally and likely performance wise is HORRIBLE and I hate looking at it like this
        //Ideally, we should switch this entire system over to some form of event based setup. Not sure if that is possible though
        void Update()
        {
            //if we dont have a player then return
            if (playerInfoStore.Owner == null)
                return;

            //continually move ourselves to the player's position
            transform.position = playerInfoStore.Owner.GetPosition();

            #region VFX
            SetParticleSystemEmission(LeftWingTrail, VFX && playerInfoStore.isGliding);
            SetParticleSystemEmission(RightWingTrail, VFX && playerInfoStore.isGliding);
            if (VFX)
            {
                //check if contributer
                if (playerInfoStore.isContributer)
                {
                    //set the trail particles to rainbow start color
                    ParticleSystem.MainModule psmain = LeftWingTrail.main;
                    psmain.startColor = gradient;

                    psmain = RightWingTrail.main;
                    psmain.startColor = gradient;
                }
                else
                {
                    //set to white
                    ParticleSystem.MainModule psmain = LeftWingTrail.main;
                    psmain.startColor = new ParticleSystem.MinMaxGradient(Color.white);

                    psmain = RightWingTrail.main;
                    psmain.startColor = new ParticleSystem.MinMaxGradient(Color.white);
                }

                if (playerInfoStore.isGliding)
                {
                    //if gliding, play the trails
                    //make sure this is before the animator updates so the trails teleport BEFORE emitting
                    //local player only. We use VRC Object syncs on the trails
                    //This is stupidly needed because we cant get the tracking data of remote players, it just returns the bone data instead
                    if (playerInfoStore.Owner.isLocal)
                    {
                        //set the wingtip transforms
                        Util.SetWingtipTransform(playerInfoStore.Owner.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand), LeftWingTrail.gameObject, playerInfoStore.avatarDetection.WingtipOffset, playerInfoStore.avatarDetection.d_spinetochest);
                        Util.SetWingtipTransform(playerInfoStore.Owner.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand), RightWingTrail.gameObject, playerInfoStore.avatarDetection.WingtipOffset, playerInfoStore.avatarDetection.d_spinetochest);
                    }
                }
            }
            #endregion

            #region SFX
            ControlSound(GlideSound, playerInfoStore.isGliding && SFX);
            if (SFX)
            {
                float playerVelocity = playerInfoStore.Owner.GetVelocity().magnitude;

                if (playerInfoStore.isGliding)
                {
                    //set the pitch of the glide sound based on the player's velocity
                    float pitch = Mathf.Lerp(minGlidePitch, maxGlidePitch, Mathf.InverseLerp(minGlideVelocity, maxGlideVelocity, playerVelocity));
                    GlideSound.pitch = pitch;
                }

                //we need to watch for the rising edge of the flap boolean
                if (playerInfoStore.isFlapping && !wasFlapping)
                {
                    //play the flap sound
                    FlapSound.PlayOneShot(FlapSound.clip);
                }
            }
            #endregion

            #region Store Previous Frame Data
            wasGliding = playerInfoStore.isGliding;
            wasFlapping = playerInfoStore.isFlapping;
            #endregion
        }

        /// <summary>
        /// Sets the emission of a particle system
        /// </summary>
        /// <param name="ps">The particle system to set the emission of</param>
        /// <param name="enabled">If the particle system should be emitting or not</param>
        private void SetParticleSystemEmission(ParticleSystem ps, bool enabled)
        {
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = enabled;
        }

        /// <summary>
        /// Controls the sound of an audio source, taking into account if it is already playing or not
        /// </summary>
        /// <param name="source">The audio source to control</param>
        /// <param name="enabled">If the audio source should be playing or not</param>
        private void ControlSound(AudioSource source, bool enabled)
        {
            if (enabled)
            {
                if (!source.isPlaying)
                {
                    source.Play();
                }
            }
            else
            {
                if (source.isPlaying)
                {
                    source.Stop();
                }
            }
        }
    }
}