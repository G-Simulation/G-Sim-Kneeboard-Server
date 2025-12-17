namespace Kneeboard_Server
{
    public class Waypoints
    {

        // HINWEIS: Für den generierten Code ist möglicherweise mindestens .NET Framework 4.5 oder .NET Core/Standard 2.0 erforderlich.
        /// <remarks/>
        [System.SerializableAttribute()]
        [System.ComponentModel.DesignerCategoryAttribute("code")]
        [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
        [System.Xml.Serialization.XmlRootAttribute("SimBase.Document", Namespace = "", IsNullable = false)]
        public partial class SimBaseDocument
        {

            private string descrField;

            private SimBaseDocumentFlightPlanFlightPlan flightPlanFlightPlanField;

            private string typeField;

            private string versionField;

            /// <remarks/>
            public string Descr
            {
                get
                {
                    return this.descrField;
                }
                set
                {
                    this.descrField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlElementAttribute("FlightPlan.FlightPlan")]
            public SimBaseDocumentFlightPlanFlightPlan FlightPlanFlightPlan
            {
                get
                {
                    return this.flightPlanFlightPlanField;
                }
                set
                {
                    this.flightPlanFlightPlanField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string Type
            {
                get
                {
                    return this.typeField;
                }
                set
                {
                    this.typeField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string version
            {
                get
                {
                    return this.versionField;
                }
                set
                {
                    this.versionField = value;
                }
            }
        }

        /// <remarks/>
        [System.SerializableAttribute()]
        [System.ComponentModel.DesignerCategoryAttribute("code")]
        [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
        public partial class SimBaseDocumentFlightPlanFlightPlan
        {

            private string titleField;

            private string fPTypeField;

            private string routeTypeField;

            private decimal cruisingAltField;

            private string departureIDField;

            private string departureLLAField;

            private string destinationIDField;

            private string destinationLLAField;

            private string descrField;

            private string departurePositionField;

            private string departureNameField;

            private string destinationNameField;

            private SimBaseDocumentFlightPlanFlightPlanAppVersion appVersionField;

            private SimBaseDocumentFlightPlanFlightPlanATCWaypoint[] aTCWaypointField;

            /// <remarks/>
            public string Title
            {
                get
                {
                    return this.titleField;
                }
                set
                {
                    this.titleField = value;
                }
            }

            /// <remarks/>
            public string FPType
            {
                get
                {
                    return this.fPTypeField;
                }
                set
                {
                    this.fPTypeField = value;
                }
            }

            /// <remarks/>
            public string RouteType
            {
                get
                {
                    return this.routeTypeField;
                }
                set
                {
                    this.routeTypeField = value;
                }
            }

            /// <remarks/>
            public decimal CruisingAlt
            {
                get
                {
                    return this.cruisingAltField;
                }
                set
                {
                    this.cruisingAltField = value;
                }
            }

            /// <remarks/>
            public string DepartureID
            {
                get
                {
                    return this.departureIDField;
                }
                set
                {
                    this.departureIDField = value;
                }
            }

            /// <remarks/>
            public string DepartureLLA
            {
                get
                {
                    return this.departureLLAField;
                }
                set
                {
                    this.departureLLAField = value;
                }
            }

            /// <remarks/>
            public string DestinationID
            {
                get
                {
                    return this.destinationIDField;
                }
                set
                {
                    this.destinationIDField = value;
                }
            }

            /// <remarks/>
            public string DestinationLLA
            {
                get
                {
                    return this.destinationLLAField;
                }
                set
                {
                    this.destinationLLAField = value;
                }
            }

            /// <remarks/>
            public string Descr
            {
                get
                {
                    return this.descrField;
                }
                set
                {
                    this.descrField = value;
                }
            }

            /// <remarks/>
            public string DeparturePosition
            {
                get
                {
                    return this.departurePositionField;
                }
                set
                {
                    this.departurePositionField = value;
                }
            }

            /// <remarks/>
            public string DepartureName
            {
                get
                {
                    return this.departureNameField;
                }
                set
                {
                    this.departureNameField = value;
                }
            }

            /// <remarks/>
            public string DestinationName
            {
                get
                {
                    return this.destinationNameField;
                }
                set
                {
                    this.destinationNameField = value;
                }
            }

            /// <remarks/>
            public SimBaseDocumentFlightPlanFlightPlanAppVersion AppVersion
            {
                get
                {
                    return this.appVersionField;
                }
                set
                {
                    this.appVersionField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlElementAttribute("ATCWaypoint")]
            public SimBaseDocumentFlightPlanFlightPlanATCWaypoint[] ATCWaypoint
            {
                get
                {
                    return this.aTCWaypointField;
                }
                set
                {
                    this.aTCWaypointField = value;
                }
            }
        }

        /// <remarks/>
        [System.SerializableAttribute()]
        [System.ComponentModel.DesignerCategoryAttribute("code")]
        [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
        public partial class SimBaseDocumentFlightPlanFlightPlanAppVersion
        {

            private byte appVersionMajorField;

            private uint appVersionBuildField;

            /// <remarks/>
            public byte AppVersionMajor
            {
                get
                {
                    return this.appVersionMajorField;
                }
                set
                {
                    this.appVersionMajorField = value;
                }
            }

            /// <remarks/>
            public uint AppVersionBuild
            {
                get
                {
                    return this.appVersionBuildField;
                }
                set
                {
                    this.appVersionBuildField = value;
                }
            }
        }

        /// <remarks/>
        [System.SerializableAttribute()]
        [System.ComponentModel.DesignerCategoryAttribute("code")]
        [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
        public partial class SimBaseDocumentFlightPlanFlightPlanATCWaypoint
        {

            private string aTCWaypointTypeField;

            private string worldPositionField;

            private ushort alt1FPField;

            private bool alt1FPFieldSpecified;

            private string altDescFPField;

            private short speedMaxFPField;

            private string arrivalFPField;

            private string aTCAirwayField;

            private string departureFPField;

            private byte runwayNumberFPField;

            private bool runwayNumberFPFieldSpecified;

            private string runwayDesignatorFPField;

            private SimBaseDocumentFlightPlanFlightPlanATCWaypointICAO iCAOField;

            private string idField;

            /// <remarks/>
            public string ATCWaypointType
            {
                get
                {
                    return this.aTCWaypointTypeField;
                }
                set
                {
                    this.aTCWaypointTypeField = value;
                }
            }

            /// <remarks/>
            public string WorldPosition
            {
                get
                {
                    return this.worldPositionField;
                }
                set
                {
                    this.worldPositionField = value;
                }
            }

            /// <remarks/>
            public ushort Alt1FP
            {
                get
                {
                    return this.alt1FPField;
                }
                set
                {
                    this.alt1FPField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlIgnoreAttribute()]
            public bool Alt1FPSpecified
            {
                get
                {
                    return this.alt1FPFieldSpecified;
                }
                set
                {
                    this.alt1FPFieldSpecified = value;
                }
            }

            /// <remarks/>
            public string AltDescFP
            {
                get
                {
                    return this.altDescFPField;
                }
                set
                {
                    this.altDescFPField = value;
                }
            }

            /// <remarks/>
            public short SpeedMaxFP
            {
                get
                {
                    return this.speedMaxFPField;
                }
                set
                {
                    this.speedMaxFPField = value;
                }
            }

            /// <remarks/>
            public string ArrivalFP
            {
                get
                {
                    return this.arrivalFPField;
                }
                set
                {
                    this.arrivalFPField = value;
                }
            }

            /// <remarks/>
            public string ATCAirway
            {
                get
                {
                    return this.aTCAirwayField;
                }
                set
                {
                    this.aTCAirwayField = value;
                }
            }

            /// <remarks/>
            public string DepartureFP
            {
                get
                {
                    return this.departureFPField;
                }
                set
                {
                    this.departureFPField = value;
                }
            }

            /// <remarks/>
            public byte RunwayNumberFP
            {
                get
                {
                    return this.runwayNumberFPField;
                }
                set
                {
                    this.runwayNumberFPField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlIgnoreAttribute()]
            public bool RunwayNumberFPSpecified
            {
                get
                {
                    return this.runwayNumberFPFieldSpecified;
                }
                set
                {
                    this.runwayNumberFPFieldSpecified = value;
                }
            }

            /// <remarks/>
            public string RunwayDesignatorFP
            {
                get
                {
                    return this.runwayDesignatorFPField;
                }
                set
                {
                    this.runwayDesignatorFPField = value;
                }
            }

            /// <remarks/>
            public SimBaseDocumentFlightPlanFlightPlanATCWaypointICAO ICAO
            {
                get
                {
                    return this.iCAOField;
                }
                set
                {
                    this.iCAOField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string id
            {
                get
                {
                    return this.idField;
                }
                set
                {
                    this.idField = value;
                }
            }
        }

        /// <remarks/>
        [System.SerializableAttribute()]
        [System.ComponentModel.DesignerCategoryAttribute("code")]
        [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
        public partial class SimBaseDocumentFlightPlanFlightPlanATCWaypointICAO
        {

            private string iCAORegionField;

            private string iCAOIdentField;

            private string iCAOAirportField;

            /// <remarks/>
            public string ICAORegion
            {
                get
                {
                    return this.iCAORegionField;
                }
                set
                {
                    this.iCAORegionField = value;
                }
            }

            /// <remarks/>
            public string ICAOIdent
            {
                get
                {
                    return this.iCAOIdentField;
                }
                set
                {
                    this.iCAOIdentField = value;
                }
            }

            /// <remarks/>
            public string ICAOAirport
            {
                get
                {
                    return this.iCAOAirportField;
                }
                set
                {
                    this.iCAOAirportField = value;
                }
            }
        }


    }
}
