﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Services.Interfaces;

namespace Aurora.Framework
{
    public class CanBeReflected : Attribute
    {
        public ThreatLevel ThreatLevel;
        public string RenamedMethod = "";
        public bool UsePassword = false;
        /// <summary>
        /// Used for helper methods, in which the method to call is not this method, but the next up the stack
        /// </summary>
        public bool NotReflectableLookUpAnotherTrace = false;

        /// <summary>
        /// The method can only be called if a parameter UserID is passed and that the user is in the requesting region
        /// </summary>
        public bool OnlyCallableIfUserInRegion = false;
    }
}
