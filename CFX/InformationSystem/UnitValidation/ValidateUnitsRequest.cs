﻿using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using CFX;
using CFX.Structures;

namespace CFX.InformationSystem.UnitValidation
{
    /// <summary>
    /// Sent from a process endpoint in order to validate the identifier of the next production unit.  
    /// Process endpoints, where configured, should send this request before allowing the next unit
    /// to enter the process. Configuration must be provided to identify the endpoint that implements 
    /// CFX.InformationSystem.UnitValidation Identification and is responsible to provide the response.
    /// </summary>
    public class ValidateUnitsRequest : CFXMessage
    {
        public ValidateUnitsRequest()
        {
            Validations = new List<ValidationType>();
            Units = new List<UnitPosition>();
        }

        /// <summary>
        /// List of validations to be performed”. Options are: UnitRouteValidation", "UnitStatusValidation"
        /// </summary>
        public List<ValidationType> Validations
        {
            get;
            set;
        }

        /// <summary>
        /// Identification used for the carrier (or the unit itself if no carrier)
        /// </summary>
        public string PrimaryIdentifier
        {
            get;
            set;
        }

        /// <summary>
        /// List of structures that identify each specific instance of production unit that arrived (could be within a carrier or panel). 
        /// </summary>
        public List<UnitPosition> Units
        {
            get;
            set;
        }
    }
}