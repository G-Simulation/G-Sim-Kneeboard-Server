using System;
using System.Xml.Serialization;
using System.Collections.Generic;

namespace Kneeboard_Server
{
    public class Simbrief
    {

        [XmlRoot(ElementName = "fetch")]
        public class Fetch
        {
            [XmlElement(ElementName = "userid")]
            public string Userid { get; set; }
            [XmlElement(ElementName = "static_id")]
            public string Static_id { get; set; }
            [XmlElement(ElementName = "status")]
            public string Status { get; set; }
            [XmlElement(ElementName = "time")]
            public string Time { get; set; }
        }

        [XmlRoot(ElementName = "params")]
        public class Params
        {
            [XmlElement(ElementName = "request_id")]
            public string Request_id { get; set; }
            [XmlElement(ElementName = "user_id")]
            public string User_id { get; set; }
            [XmlElement(ElementName = "time_generated")]
            public string Time_generated { get; set; }
            [XmlElement(ElementName = "static_id")]
            public string Static_id { get; set; }
            [XmlElement(ElementName = "ofp_layout")]
            public string Ofp_layout { get; set; }
            [XmlElement(ElementName = "airac")]
            public string Airac { get; set; }
            [XmlElement(ElementName = "units")]
            public string Units { get; set; }
        }

        [XmlRoot(ElementName = "general")]
        public class General
        {
            [XmlElement(ElementName = "release")]
            public string Release { get; set; }
            [XmlElement(ElementName = "icao_airline")]
            public string Icao_airline { get; set; }
            [XmlElement(ElementName = "flight_number")]
            public string Flight_number { get; set; }
            [XmlElement(ElementName = "is_etops")]
            public string Is_etops { get; set; }
            [XmlElement(ElementName = "dx_rmk")]
            public string Dx_rmk { get; set; }
            [XmlElement(ElementName = "sys_rmk")]
            public string Sys_rmk { get; set; }
            [XmlElement(ElementName = "is_detailed_profile")]
            public string Is_detailed_profile { get; set; }
            [XmlElement(ElementName = "cruise_profile")]
            public string Cruise_profile { get; set; }
            [XmlElement(ElementName = "climb_profile")]
            public string Climb_profile { get; set; }
            [XmlElement(ElementName = "descent_profile")]
            public string Descent_profile { get; set; }
            [XmlElement(ElementName = "alternate_profile")]
            public string Alternate_profile { get; set; }
            [XmlElement(ElementName = "reserve_profile")]
            public string Reserve_profile { get; set; }
            [XmlElement(ElementName = "costindex")]
            public string Costindex { get; set; }
            [XmlElement(ElementName = "cont_rule")]
            public string Cont_rule { get; set; }
            [XmlElement(ElementName = "initial_altitude")]
            public string Initial_altitude { get; set; }
            [XmlElement(ElementName = "stepclimb_string")]
            public string Stepclimb_string { get; set; }
            [XmlElement(ElementName = "avg_temp_dev")]
            public string Avg_temp_dev { get; set; }
            [XmlElement(ElementName = "avg_tropopause")]
            public string Avg_tropopause { get; set; }
            [XmlElement(ElementName = "avg_wind_comp")]
            public string Avg_wind_comp { get; set; }
            [XmlElement(ElementName = "avg_wind_dir")]
            public string Avg_wind_dir { get; set; }
            [XmlElement(ElementName = "avg_wind_spd")]
            public string Avg_wind_spd { get; set; }
            [XmlElement(ElementName = "gc_distance")]
            public string Gc_distance { get; set; }
            [XmlElement(ElementName = "route_distance")]
            public string Route_distance { get; set; }
            [XmlElement(ElementName = "air_distance")]
            public string Air_distance { get; set; }
            [XmlElement(ElementName = "total_burn")]
            public string Total_burn { get; set; }
            [XmlElement(ElementName = "cruise_tas")]
            public string Cruise_tas { get; set; }
            [XmlElement(ElementName = "cruise_mach")]
            public string Cruise_mach { get; set; }
            [XmlElement(ElementName = "passengers")]
            public string Passengers { get; set; }
            [XmlElement(ElementName = "route")]
            public string Route { get; set; }
            [XmlElement(ElementName = "route_ifps")]
            public string Route_ifps { get; set; }
            [XmlElement(ElementName = "route_navigraph")]
            public string Route_navigraph { get; set; }
        }

        [XmlRoot(ElementName = "atis")]
        public class Atis
        {
            [XmlElement(ElementName = "network")]
            public string Network { get; set; }
            [XmlElement(ElementName = "issued")]
            public string Issued { get; set; }
            [XmlElement(ElementName = "letter")]
            public string Letter { get; set; }
            [XmlElement(ElementName = "phonetic")]
            public string Phonetic { get; set; }
            [XmlElement(ElementName = "type")]
            public string Type { get; set; }
            [XmlElement(ElementName = "message")]
            public string Message { get; set; }
        }

        [XmlRoot(ElementName = "origin")]
        public class Origin
        {
            [XmlElement(ElementName = "icao_code")]
            public string Icao_code { get; set; }
            [XmlElement(ElementName = "iata_code")]
            public string Iata_code { get; set; }
            [XmlElement(ElementName = "faa_code")]
            public string Faa_code { get; set; }
            [XmlElement(ElementName = "elevation")]
            public string Elevation { get; set; }
            [XmlElement(ElementName = "pos_lat")]
            public string Pos_lat { get; set; }
            [XmlElement(ElementName = "pos_long")]
            public string Pos_long { get; set; }
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "plan_rwy")]
            public string Plan_rwy { get; set; }
            [XmlElement(ElementName = "trans_alt")]
            public string Trans_alt { get; set; }
            [XmlElement(ElementName = "trans_level")]
            public string Trans_level { get; set; }
            [XmlElement(ElementName = "metar")]
            public string Metar { get; set; }
            [XmlElement(ElementName = "taf")]
            public string Taf { get; set; }
            [XmlElement(ElementName = "atis")]
            public List<Atis> Atis { get; set; }
            [XmlElement(ElementName = "notams")]
            public string Notams { get; set; }
        }

        [XmlRoot(ElementName = "destination")]
        public class Destination
        {
            [XmlElement(ElementName = "icao_code")]
            public string Icao_code { get; set; }
            [XmlElement(ElementName = "iata_code")]
            public string Iata_code { get; set; }
            [XmlElement(ElementName = "faa_code")]
            public string Faa_code { get; set; }
            [XmlElement(ElementName = "elevation")]
            public string Elevation { get; set; }
            [XmlElement(ElementName = "pos_lat")]
            public string Pos_lat { get; set; }
            [XmlElement(ElementName = "pos_long")]
            public string Pos_long { get; set; }
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "plan_rwy")]
            public string Plan_rwy { get; set; }
            [XmlElement(ElementName = "trans_alt")]
            public string Trans_alt { get; set; }
            [XmlElement(ElementName = "trans_level")]
            public string Trans_level { get; set; }
            [XmlElement(ElementName = "metar")]
            public string Metar { get; set; }
            [XmlElement(ElementName = "taf")]
            public string Taf { get; set; }
            [XmlElement(ElementName = "atis")]
            public List<Atis> Atis { get; set; }
            [XmlElement(ElementName = "notams")]
            public string Notams { get; set; }
        }

        [XmlRoot(ElementName = "alternate")]
        public class Alternate
        {
            [XmlElement(ElementName = "icao_code")]
            public string Icao_code { get; set; }
            [XmlElement(ElementName = "iata_code")]
            public string Iata_code { get; set; }
            [XmlElement(ElementName = "faa_code")]
            public string Faa_code { get; set; }
            [XmlElement(ElementName = "elevation")]
            public string Elevation { get; set; }
            [XmlElement(ElementName = "pos_lat")]
            public string Pos_lat { get; set; }
            [XmlElement(ElementName = "pos_long")]
            public string Pos_long { get; set; }
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "plan_rwy")]
            public string Plan_rwy { get; set; }
            [XmlElement(ElementName = "trans_alt")]
            public string Trans_alt { get; set; }
            [XmlElement(ElementName = "trans_level")]
            public string Trans_level { get; set; }
            [XmlElement(ElementName = "cruise_altitude")]
            public string Cruise_altitude { get; set; }
            [XmlElement(ElementName = "distance")]
            public string Distance { get; set; }
            [XmlElement(ElementName = "gc_distance")]
            public string Gc_distance { get; set; }
            [XmlElement(ElementName = "air_distance")]
            public string Air_distance { get; set; }
            [XmlElement(ElementName = "track_true")]
            public string Track_true { get; set; }
            [XmlElement(ElementName = "track_mag")]
            public string Track_mag { get; set; }
            [XmlElement(ElementName = "tas")]
            public string Tas { get; set; }
            [XmlElement(ElementName = "gs")]
            public string Gs { get; set; }
            [XmlElement(ElementName = "avg_wind_comp")]
            public string Avg_wind_comp { get; set; }
            [XmlElement(ElementName = "avg_wind_dir")]
            public string Avg_wind_dir { get; set; }
            [XmlElement(ElementName = "avg_wind_spd")]
            public string Avg_wind_spd { get; set; }
            [XmlElement(ElementName = "avg_tropopause")]
            public string Avg_tropopause { get; set; }
            [XmlElement(ElementName = "avg_tdv")]
            public string Avg_tdv { get; set; }
            [XmlElement(ElementName = "ete")]
            public string Ete { get; set; }
            [XmlElement(ElementName = "burn")]
            public string Burn { get; set; }
            [XmlElement(ElementName = "route")]
            public string Route { get; set; }
            [XmlElement(ElementName = "route_ifps")]
            public string Route_ifps { get; set; }
            [XmlElement(ElementName = "metar")]
            public string Metar { get; set; }
            [XmlElement(ElementName = "taf")]
            public string Taf { get; set; }
            [XmlElement(ElementName = "atis")]
            public Atis Atis { get; set; }
            [XmlElement(ElementName = "notams")]
            public string Notams { get; set; }
        }

        [XmlRoot(ElementName = "level")]
        public class Level
        {
            [XmlElement(ElementName = "altitude")]
            public string Altitude { get; set; }
            [XmlElement(ElementName = "wind_dir")]
            public string Wind_dir { get; set; }
            [XmlElement(ElementName = "wind_spd")]
            public string Wind_spd { get; set; }
            [XmlElement(ElementName = "oat")]
            public string Oat { get; set; }
        }

        [XmlRoot(ElementName = "wind_data")]
        public class Wind_data
        {
            [XmlElement(ElementName = "level")]
            public List<Level> Level { get; set; }
        }

        [XmlRoot(ElementName = "fix")]
        public class Fix
        {
            [XmlElement(ElementName = "ident")]
            public string Ident { get; set; }
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "type")]
            public string Type { get; set; }
            [XmlElement(ElementName = "frequency")]
            public string Frequency { get; set; }
            [XmlElement(ElementName = "pos_lat")]
            public string Pos_lat { get; set; }
            [XmlElement(ElementName = "pos_long")]
            public string Pos_long { get; set; }
            [XmlElement(ElementName = "stage")]
            public string Stage { get; set; }
            [XmlElement(ElementName = "via_airway")]
            public string Via_airway { get; set; }
            [XmlElement(ElementName = "is_sid_star")]
            public string Is_sid_star { get; set; }
            [XmlElement(ElementName = "distance")]
            public string Distance { get; set; }
            [XmlElement(ElementName = "track_true")]
            public string Track_true { get; set; }
            [XmlElement(ElementName = "track_mag")]
            public string Track_mag { get; set; }
            [XmlElement(ElementName = "heading_true")]
            public string Heading_true { get; set; }
            [XmlElement(ElementName = "heading_mag")]
            public string Heading_mag { get; set; }
            [XmlElement(ElementName = "altitude_feet")]
            public string Altitude_feet { get; set; }
            [XmlElement(ElementName = "ind_airspeed")]
            public string Ind_airspeed { get; set; }
            [XmlElement(ElementName = "true_airspeed")]
            public string True_airspeed { get; set; }
            [XmlElement(ElementName = "mach")]
            public string Mach { get; set; }
            [XmlElement(ElementName = "mach_thousandths")]
            public string Mach_thousandths { get; set; }
            [XmlElement(ElementName = "wind_component")]
            public string Wind_component { get; set; }
            [XmlElement(ElementName = "groundspeed")]
            public string Groundspeed { get; set; }
            [XmlElement(ElementName = "time_leg")]
            public string Time_leg { get; set; }
            [XmlElement(ElementName = "time_total")]
            public string Time_total { get; set; }
            [XmlElement(ElementName = "fuel_flow")]
            public string Fuel_flow { get; set; }
            [XmlElement(ElementName = "fuel_leg")]
            public string Fuel_leg { get; set; }
            [XmlElement(ElementName = "fuel_totalused")]
            public string Fuel_totalused { get; set; }
            [XmlElement(ElementName = "fuel_min_onboard")]
            public string Fuel_min_onboard { get; set; }
            [XmlElement(ElementName = "fuel_plan_onboard")]
            public string Fuel_plan_onboard { get; set; }
            [XmlElement(ElementName = "oat")]
            public string Oat { get; set; }
            [XmlElement(ElementName = "oat_isa_dev")]
            public string Oat_isa_dev { get; set; }
            [XmlElement(ElementName = "wind_dir")]
            public string Wind_dir { get; set; }
            [XmlElement(ElementName = "wind_spd")]
            public string Wind_spd { get; set; }
            [XmlElement(ElementName = "shear")]
            public string Shear { get; set; }
            [XmlElement(ElementName = "tropopause_feet")]
            public string Tropopause_feet { get; set; }
            [XmlElement(ElementName = "ground_height")]
            public string Ground_height { get; set; }
            [XmlElement(ElementName = "mora")]
            public string Mora { get; set; }
            [XmlElement(ElementName = "fir")]
            public string Fir { get; set; }
            [XmlElement(ElementName = "fir_units")]
            public string Fir_units { get; set; }
            [XmlElement(ElementName = "fir_valid_levels")]
            public string Fir_valid_levels { get; set; }
            [XmlElement(ElementName = "wind_data")]
            public Wind_data Wind_data { get; set; }
            [XmlElement(ElementName = "fir_crossing")]
            public Fir_crossing Fir_crossing { get; set; }
        }

        [XmlRoot(ElementName = "fir")]
        public class Fir
        {
            [XmlElement(ElementName = "fir_icao")]
            public string Fir_icao { get; set; }
            [XmlElement(ElementName = "fir_name")]
            public string Fir_name { get; set; }
            [XmlElement(ElementName = "pos_lat_entry")]
            public string Pos_lat_entry { get; set; }
            [XmlElement(ElementName = "pos_long_entry")]
            public string Pos_long_entry { get; set; }
        }

        [XmlRoot(ElementName = "fir_crossing")]
        public class Fir_crossing
        {
            [XmlElement(ElementName = "fir")]
            public Fir Fir { get; set; }
        }

        [XmlRoot(ElementName = "navlog")]
        public class Navlog
        {
            [XmlElement(ElementName = "fix")]
            public List<Fix> Fix { get; set; }
        }

        [XmlRoot(ElementName = "atc")]
        public class Atc
        {
            [XmlElement(ElementName = "flightplan_text")]
            public string Flightplan_text { get; set; }
            [XmlElement(ElementName = "route")]
            public string Route { get; set; }
            [XmlElement(ElementName = "route_ifps")]
            public string Route_ifps { get; set; }
            [XmlElement(ElementName = "callsign")]
            public string Callsign { get; set; }
            [XmlElement(ElementName = "initial_spd")]
            public string Initial_spd { get; set; }
            [XmlElement(ElementName = "initial_spd_unit")]
            public string Initial_spd_unit { get; set; }
            [XmlElement(ElementName = "initial_alt")]
            public string Initial_alt { get; set; }
            [XmlElement(ElementName = "initial_alt_unit")]
            public string Initial_alt_unit { get; set; }
            [XmlElement(ElementName = "section18")]
            public string Section18 { get; set; }
            [XmlElement(ElementName = "fir_orig")]
            public string Fir_orig { get; set; }
            [XmlElement(ElementName = "fir_dest")]
            public string Fir_dest { get; set; }
            [XmlElement(ElementName = "fir_altn")]
            public string Fir_altn { get; set; }
            [XmlElement(ElementName = "fir_etops")]
            public string Fir_etops { get; set; }
            [XmlElement(ElementName = "fir_enroute")]
            public string Fir_enroute { get; set; }
        }

        [XmlRoot(ElementName = "aircraft")]
        public class Aircraft
        {
            [XmlElement(ElementName = "icaocode")]
            public string Icaocode { get; set; }
            [XmlElement(ElementName = "iatacode")]
            public string Iatacode { get; set; }
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "reg")]
            public string Reg { get; set; }
            [XmlElement(ElementName = "fin")]
            public string Fin { get; set; }
            [XmlElement(ElementName = "selcal")]
            public string Selcal { get; set; }
            [XmlElement(ElementName = "equip")]
            public string Equip { get; set; }
            [XmlElement(ElementName = "max_passengers")]
            public string Max_passengers { get; set; }
            [XmlElement(ElementName = "fuelfact")]
            public string Fuelfact { get; set; }
        }

        [XmlRoot(ElementName = "fuel")]
        public class Fuel
        {
            [XmlElement(ElementName = "taxi")]
            public string Taxi { get; set; }
            [XmlElement(ElementName = "enroute_burn")]
            public string Enroute_burn { get; set; }
            [XmlElement(ElementName = "contingency")]
            public string Contingency { get; set; }
            [XmlElement(ElementName = "alternate_burn")]
            public string Alternate_burn { get; set; }
            [XmlElement(ElementName = "reserve")]
            public string Reserve { get; set; }
            [XmlElement(ElementName = "etops")]
            public string Etops { get; set; }
            [XmlElement(ElementName = "extra")]
            public string Extra { get; set; }
            [XmlElement(ElementName = "min_takeoff")]
            public string Min_takeoff { get; set; }
            [XmlElement(ElementName = "plan_takeoff")]
            public string Plan_takeoff { get; set; }
            [XmlElement(ElementName = "plan_ramp")]
            public string Plan_ramp { get; set; }
            [XmlElement(ElementName = "plan_landing")]
            public string Plan_landing { get; set; }
            [XmlElement(ElementName = "avg_fuel_flow")]
            public string Avg_fuel_flow { get; set; }
            [XmlElement(ElementName = "max_tanks")]
            public string Max_tanks { get; set; }
        }

        [XmlRoot(ElementName = "times")]
        public class Times
        {
            [XmlElement(ElementName = "est_time_enroute")]
            public string Est_time_enroute { get; set; }
            [XmlElement(ElementName = "sched_time_enroute")]
            public string Sched_time_enroute { get; set; }
            [XmlElement(ElementName = "sched_out")]
            public string Sched_out { get; set; }
            [XmlElement(ElementName = "sched_off")]
            public string Sched_off { get; set; }
            [XmlElement(ElementName = "sched_on")]
            public string Sched_on { get; set; }
            [XmlElement(ElementName = "sched_in")]
            public string Sched_in { get; set; }
            [XmlElement(ElementName = "sched_block")]
            public string Sched_block { get; set; }
            [XmlElement(ElementName = "est_out")]
            public string Est_out { get; set; }
            [XmlElement(ElementName = "est_off")]
            public string Est_off { get; set; }
            [XmlElement(ElementName = "est_on")]
            public string Est_on { get; set; }
            [XmlElement(ElementName = "est_in")]
            public string Est_in { get; set; }
            [XmlElement(ElementName = "est_block")]
            public string Est_block { get; set; }
            [XmlElement(ElementName = "orig_timezone")]
            public string Orig_timezone { get; set; }
            [XmlElement(ElementName = "dest_timezone")]
            public string Dest_timezone { get; set; }
            [XmlElement(ElementName = "taxi_out")]
            public string Taxi_out { get; set; }
            [XmlElement(ElementName = "taxi_in")]
            public string Taxi_in { get; set; }
            [XmlElement(ElementName = "reserve_time")]
            public string Reserve_time { get; set; }
            [XmlElement(ElementName = "endurance")]
            public string Endurance { get; set; }
            [XmlElement(ElementName = "contfuel_time")]
            public string Contfuel_time { get; set; }
            [XmlElement(ElementName = "etopsfuel_time")]
            public string Etopsfuel_time { get; set; }
            [XmlElement(ElementName = "extrafuel_time")]
            public string Extrafuel_time { get; set; }
        }

        [XmlRoot(ElementName = "weights")]
        public class Weights
        {
            [XmlElement(ElementName = "oew")]
            public string Oew { get; set; }
            [XmlElement(ElementName = "pax_count")]
            public string Pax_count { get; set; }
            [XmlElement(ElementName = "bag_count")]
            public string Bag_count { get; set; }
            [XmlElement(ElementName = "pax_count_actual")]
            public string Pax_count_actual { get; set; }
            [XmlElement(ElementName = "bag_count_actual")]
            public string Bag_count_actual { get; set; }
            [XmlElement(ElementName = "pax_weight")]
            public string Pax_weight { get; set; }
            [XmlElement(ElementName = "bag_weight")]
            public string Bag_weight { get; set; }
            [XmlElement(ElementName = "freight_added")]
            public string Freight_added { get; set; }
            [XmlElement(ElementName = "cargo")]
            public string Cargo { get; set; }
            [XmlElement(ElementName = "payload")]
            public string Payload { get; set; }
            [XmlElement(ElementName = "est_zfw")]
            public string Est_zfw { get; set; }
            [XmlElement(ElementName = "max_zfw")]
            public string Max_zfw { get; set; }
            [XmlElement(ElementName = "est_tow")]
            public string Est_tow { get; set; }
            [XmlElement(ElementName = "max_tow")]
            public string Max_tow { get; set; }
            [XmlElement(ElementName = "max_tow_struct")]
            public string Max_tow_struct { get; set; }
            [XmlElement(ElementName = "tow_limit_code")]
            public string Tow_limit_code { get; set; }
            [XmlElement(ElementName = "est_ldw")]
            public string Est_ldw { get; set; }
            [XmlElement(ElementName = "max_ldw")]
            public string Max_ldw { get; set; }
            [XmlElement(ElementName = "est_ramp")]
            public string Est_ramp { get; set; }
        }

        [XmlRoot(ElementName = "minus_6000ft")]
        public class Minus_6000ft
        {
            [XmlElement(ElementName = "time_enroute")]
            public string Time_enroute { get; set; }
            [XmlElement(ElementName = "time_difference")]
            public string Time_difference { get; set; }
            [XmlElement(ElementName = "enroute_burn")]
            public string Enroute_burn { get; set; }
            [XmlElement(ElementName = "burn_difference")]
            public string Burn_difference { get; set; }
            [XmlElement(ElementName = "ramp_fuel")]
            public string Ramp_fuel { get; set; }
            [XmlElement(ElementName = "initial_fl")]
            public string Initial_fl { get; set; }
            [XmlElement(ElementName = "initial_tas")]
            public string Initial_tas { get; set; }
            [XmlElement(ElementName = "initial_mach")]
            public string Initial_mach { get; set; }
            [XmlElement(ElementName = "cost_index")]
            public string Cost_index { get; set; }
        }

        [XmlRoot(ElementName = "minus_4000ft")]
        public class Minus_4000ft
        {
            [XmlElement(ElementName = "time_enroute")]
            public string Time_enroute { get; set; }
            [XmlElement(ElementName = "time_difference")]
            public string Time_difference { get; set; }
            [XmlElement(ElementName = "enroute_burn")]
            public string Enroute_burn { get; set; }
            [XmlElement(ElementName = "burn_difference")]
            public string Burn_difference { get; set; }
            [XmlElement(ElementName = "ramp_fuel")]
            public string Ramp_fuel { get; set; }
            [XmlElement(ElementName = "initial_fl")]
            public string Initial_fl { get; set; }
            [XmlElement(ElementName = "initial_tas")]
            public string Initial_tas { get; set; }
            [XmlElement(ElementName = "initial_mach")]
            public string Initial_mach { get; set; }
            [XmlElement(ElementName = "cost_index")]
            public string Cost_index { get; set; }
        }

        [XmlRoot(ElementName = "minus_2000ft")]
        public class Minus_2000ft
        {
            [XmlElement(ElementName = "time_enroute")]
            public string Time_enroute { get; set; }
            [XmlElement(ElementName = "time_difference")]
            public string Time_difference { get; set; }
            [XmlElement(ElementName = "enroute_burn")]
            public string Enroute_burn { get; set; }
            [XmlElement(ElementName = "burn_difference")]
            public string Burn_difference { get; set; }
            [XmlElement(ElementName = "ramp_fuel")]
            public string Ramp_fuel { get; set; }
            [XmlElement(ElementName = "initial_fl")]
            public string Initial_fl { get; set; }
            [XmlElement(ElementName = "initial_tas")]
            public string Initial_tas { get; set; }
            [XmlElement(ElementName = "initial_mach")]
            public string Initial_mach { get; set; }
            [XmlElement(ElementName = "cost_index")]
            public string Cost_index { get; set; }
        }

        [XmlRoot(ElementName = "higher_ci")]
        public class Higher_ci
        {
            [XmlElement(ElementName = "time_enroute")]
            public string Time_enroute { get; set; }
            [XmlElement(ElementName = "time_difference")]
            public string Time_difference { get; set; }
            [XmlElement(ElementName = "enroute_burn")]
            public string Enroute_burn { get; set; }
            [XmlElement(ElementName = "burn_difference")]
            public string Burn_difference { get; set; }
            [XmlElement(ElementName = "ramp_fuel")]
            public string Ramp_fuel { get; set; }
            [XmlElement(ElementName = "initial_fl")]
            public string Initial_fl { get; set; }
            [XmlElement(ElementName = "initial_tas")]
            public string Initial_tas { get; set; }
            [XmlElement(ElementName = "initial_mach")]
            public string Initial_mach { get; set; }
            [XmlElement(ElementName = "cost_index")]
            public string Cost_index { get; set; }
        }

        [XmlRoot(ElementName = "lower_ci")]
        public class Lower_ci
        {
            [XmlElement(ElementName = "time_enroute")]
            public string Time_enroute { get; set; }
            [XmlElement(ElementName = "time_difference")]
            public string Time_difference { get; set; }
            [XmlElement(ElementName = "enroute_burn")]
            public string Enroute_burn { get; set; }
            [XmlElement(ElementName = "burn_difference")]
            public string Burn_difference { get; set; }
            [XmlElement(ElementName = "ramp_fuel")]
            public string Ramp_fuel { get; set; }
            [XmlElement(ElementName = "initial_fl")]
            public string Initial_fl { get; set; }
            [XmlElement(ElementName = "initial_tas")]
            public string Initial_tas { get; set; }
            [XmlElement(ElementName = "initial_mach")]
            public string Initial_mach { get; set; }
            [XmlElement(ElementName = "cost_index")]
            public string Cost_index { get; set; }
        }

        [XmlRoot(ElementName = "zfw_plus_1000")]
        public class Zfw_plus_1000
        {
            [XmlElement(ElementName = "time_enroute")]
            public string Time_enroute { get; set; }
            [XmlElement(ElementName = "time_difference")]
            public string Time_difference { get; set; }
            [XmlElement(ElementName = "enroute_burn")]
            public string Enroute_burn { get; set; }
            [XmlElement(ElementName = "burn_difference")]
            public string Burn_difference { get; set; }
            [XmlElement(ElementName = "ramp_fuel")]
            public string Ramp_fuel { get; set; }
            [XmlElement(ElementName = "initial_fl")]
            public string Initial_fl { get; set; }
            [XmlElement(ElementName = "initial_tas")]
            public string Initial_tas { get; set; }
            [XmlElement(ElementName = "initial_mach")]
            public string Initial_mach { get; set; }
            [XmlElement(ElementName = "cost_index")]
            public string Cost_index { get; set; }
        }

        [XmlRoot(ElementName = "zfw_minus_1000")]
        public class Zfw_minus_1000
        {
            [XmlElement(ElementName = "time_enroute")]
            public string Time_enroute { get; set; }
            [XmlElement(ElementName = "time_difference")]
            public string Time_difference { get; set; }
            [XmlElement(ElementName = "enroute_burn")]
            public string Enroute_burn { get; set; }
            [XmlElement(ElementName = "burn_difference")]
            public string Burn_difference { get; set; }
            [XmlElement(ElementName = "ramp_fuel")]
            public string Ramp_fuel { get; set; }
            [XmlElement(ElementName = "initial_fl")]
            public string Initial_fl { get; set; }
            [XmlElement(ElementName = "initial_tas")]
            public string Initial_tas { get; set; }
            [XmlElement(ElementName = "initial_mach")]
            public string Initial_mach { get; set; }
            [XmlElement(ElementName = "cost_index")]
            public string Cost_index { get; set; }
        }

        [XmlRoot(ElementName = "impacts")]
        public class Impacts
        {
            [XmlElement(ElementName = "minus_6000ft")]
            public Minus_6000ft Minus_6000ft { get; set; }
            [XmlElement(ElementName = "minus_4000ft")]
            public Minus_4000ft Minus_4000ft { get; set; }
            [XmlElement(ElementName = "minus_2000ft")]
            public Minus_2000ft Minus_2000ft { get; set; }
            [XmlElement(ElementName = "plus_2000ft")]
            public string Plus_2000ft { get; set; }
            [XmlElement(ElementName = "plus_4000ft")]
            public string Plus_4000ft { get; set; }
            [XmlElement(ElementName = "plus_6000ft")]
            public string Plus_6000ft { get; set; }
            [XmlElement(ElementName = "higher_ci")]
            public Higher_ci Higher_ci { get; set; }
            [XmlElement(ElementName = "lower_ci")]
            public Lower_ci Lower_ci { get; set; }
            [XmlElement(ElementName = "zfw_plus_1000")]
            public Zfw_plus_1000 Zfw_plus_1000 { get; set; }
            [XmlElement(ElementName = "zfw_minus_1000")]
            public Zfw_minus_1000 Zfw_minus_1000 { get; set; }
        }

        [XmlRoot(ElementName = "crew")]
        public class Crew
        {
            [XmlElement(ElementName = "pilot_id")]
            public string Pilot_id { get; set; }
            [XmlElement(ElementName = "cpt")]
            public string Cpt { get; set; }
            [XmlElement(ElementName = "fo")]
            public string Fo { get; set; }
            [XmlElement(ElementName = "dx")]
            public string Dx { get; set; }
            [XmlElement(ElementName = "pu")]
            public string Pu { get; set; }
            [XmlElement(ElementName = "fa")]
            public List<string> Fa { get; set; }
        }

        [XmlRoot(ElementName = "notamdrec")]
        public class Notamdrec
        {
            [XmlElement(ElementName = "source_id")]
            public string Source_id { get; set; }
            [XmlElement(ElementName = "account_id")]
            public string Account_id { get; set; }
            [XmlElement(ElementName = "notam_id")]
            public string Notam_id { get; set; }
            [XmlElement(ElementName = "notam_part")]
            public string Notam_part { get; set; }
            [XmlElement(ElementName = "cns_location_id")]
            public string Cns_location_id { get; set; }
            [XmlElement(ElementName = "icao_id")]
            public string Icao_id { get; set; }
            [XmlElement(ElementName = "icao_name")]
            public string Icao_name { get; set; }
            [XmlElement(ElementName = "total_parts")]
            public string Total_parts { get; set; }
            [XmlElement(ElementName = "notam_created_dtg")]
            public string Notam_created_dtg { get; set; }
            [XmlElement(ElementName = "notam_effective_dtg")]
            public string Notam_effective_dtg { get; set; }
            [XmlElement(ElementName = "notam_expire_dtg")]
            public string Notam_expire_dtg { get; set; }
            [XmlElement(ElementName = "notam_lastmod_dtg")]
            public string Notam_lastmod_dtg { get; set; }
            [XmlElement(ElementName = "notam_inserted_dtg")]
            public string Notam_inserted_dtg { get; set; }
            [XmlElement(ElementName = "notam_text")]
            public string Notam_text { get; set; }
            [XmlElement(ElementName = "notam_report")]
            public string Notam_report { get; set; }
            [XmlElement(ElementName = "notam_nrc")]
            public string Notam_nrc { get; set; }
            [XmlElement(ElementName = "notam_qcode")]
            public string Notam_qcode { get; set; }
        }

        [XmlRoot(ElementName = "notams")]
        public class Notams
        {
            [XmlElement(ElementName = "notamdrec")]
            public List<Notamdrec> Notamdrec { get; set; }
            [XmlElement(ElementName = "rec-count")]
            public string Reccount { get; set; }
        }

        [XmlRoot(ElementName = "weather")]
        public class Weather
        {
            [XmlElement(ElementName = "orig_metar")]
            public string Orig_metar { get; set; }
            [XmlElement(ElementName = "orig_taf")]
            public string Orig_taf { get; set; }
            [XmlElement(ElementName = "dest_metar")]
            public string Dest_metar { get; set; }
            [XmlElement(ElementName = "dest_taf")]
            public string Dest_taf { get; set; }
            [XmlElement(ElementName = "altn_metar")]
            public string Altn_metar { get; set; }
            [XmlElement(ElementName = "altn_taf")]
            public string Altn_taf { get; set; }
            [XmlElement(ElementName = "toaltn_metar")]
            public string Toaltn_metar { get; set; }
            [XmlElement(ElementName = "toaltn_taf")]
            public string Toaltn_taf { get; set; }
            [XmlElement(ElementName = "eualtn_metar")]
            public string Eualtn_metar { get; set; }
            [XmlElement(ElementName = "eualtn_taf")]
            public string Eualtn_taf { get; set; }
            [XmlElement(ElementName = "etops_metar")]
            public string Etops_metar { get; set; }
            [XmlElement(ElementName = "etops_taf")]
            public string Etops_taf { get; set; }
        }

        [XmlRoot(ElementName = "text")]
        public class Text
        {
            [XmlElement(ElementName = "nat_tracks")]
            public string Nat_tracks { get; set; }
            [XmlElement(ElementName = "plan_html")]
            public string Plan_html { get; set; }
        }

        [XmlRoot(ElementName = "database_updates")]
        public class Database_updates
        {
            [XmlElement(ElementName = "metar_taf")]
            public string Metar_taf { get; set; }
            [XmlElement(ElementName = "winds")]
            public string Winds { get; set; }
            [XmlElement(ElementName = "sigwx")]
            public string Sigwx { get; set; }
            [XmlElement(ElementName = "sigmet")]
            public string Sigmet { get; set; }
            [XmlElement(ElementName = "notams")]
            public string Notams { get; set; }
            [XmlElement(ElementName = "tracks")]
            public string Tracks { get; set; }
        }

        [XmlRoot(ElementName = "pdf")]
        public class Pdf
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "file")]
        public class File
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "files")]
        public class Files
        {
            [XmlElement(ElementName = "directory")]
            public string Directory { get; set; }
            [XmlElement(ElementName = "pdf")]
            public Pdf Pdf { get; set; }
            [XmlElement(ElementName = "file")]
            public List<File> File { get; set; }
        }

        [XmlRoot(ElementName = "abx")]
        public class Abx
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "a3e")]
        public class A3e
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "crx")]
        public class Crx
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "cra")]
        public class Cra
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "psx")]
        public class Psx
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "efb")]
        public class Efb
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "ef2")]
        public class Ef2
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "bbs")]
        public class Bbs
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "csf")]
        public class Csf
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "ftr")]
        public class Ftr
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "gtn")]
        public class Gtn
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "vm5")]
        public class Vm5
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "vmx")]
        public class Vmx
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "ffa")]
        public class Ffa
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "fsc")]
        public class Fsc
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "fs9")]
        public class Fs9
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "mfs")]
        public class Mfs
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "fsl")]
        public class Fsl
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "fsx")]
        public class Fsx
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "fsn")]
        public class Fsn
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "kml")]
        public class Kml
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "ify")]
        public class Ify
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "i74")]
        public class I74
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "ifa")]
        public class Ifa
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "inb")]
        public class Inb
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "ivo")]
        public class Ivo
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "xvd")]
        public class Xvd
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "xvp")]
        public class Xvp
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "ixg")]
        public class Ixg
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "jar")]
        public class Jar
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "jhe")]
        public class Jhe
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "mdr")]
        public class Mdr
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "mda")]
        public class Mda
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "lvd")]
        public class Lvd
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "mjc")]
        public class Mjc
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "mvz")]
        public class Mvz
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "vms")]
        public class Vms
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "pmo")]
        public class Pmo
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "pmr")]
        public class Pmr
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "pmw")]
        public class Pmw
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "pgt")]
        public class Pgt
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "mga")]
        public class Mga
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "psm")]
        public class Psm
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "qty")]
        public class Qty
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "rmd")]
        public class Rmd
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "sbr")]
        public class Sbr
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "sfp")]
        public class Sfp
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "tdg")]
        public class Tdg
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "tfd")]
        public class Tfd
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "ufc")]
        public class Ufc
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "vas")]
        public class Vas
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "vfp")]
        public class Vfp
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "wae")]
        public class Wae
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "xfm")]
        public class Xfm
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "xpe")]
        public class Xpe
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "xp9")]
        public class Xp9
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "fms_downloads")]
        public class Fms_downloads
        {
            [XmlElement(ElementName = "directory")]
            public string Directory { get; set; }
            [XmlElement(ElementName = "pdf")]
            public Pdf Pdf { get; set; }
            [XmlElement(ElementName = "abx")]
            public Abx Abx { get; set; }
            [XmlElement(ElementName = "a3e")]
            public A3e A3e { get; set; }
            [XmlElement(ElementName = "crx")]
            public Crx Crx { get; set; }
            [XmlElement(ElementName = "cra")]
            public Cra Cra { get; set; }
            [XmlElement(ElementName = "psx")]
            public Psx Psx { get; set; }
            [XmlElement(ElementName = "efb")]
            public Efb Efb { get; set; }
            [XmlElement(ElementName = "ef2")]
            public Ef2 Ef2 { get; set; }
            [XmlElement(ElementName = "bbs")]
            public Bbs Bbs { get; set; }
            [XmlElement(ElementName = "csf")]
            public Csf Csf { get; set; }
            [XmlElement(ElementName = "ftr")]
            public Ftr Ftr { get; set; }
            [XmlElement(ElementName = "gtn")]
            public Gtn Gtn { get; set; }
            [XmlElement(ElementName = "vm5")]
            public Vm5 Vm5 { get; set; }
            [XmlElement(ElementName = "vmx")]
            public Vmx Vmx { get; set; }
            [XmlElement(ElementName = "ffa")]
            public Ffa Ffa { get; set; }
            [XmlElement(ElementName = "fsc")]
            public Fsc Fsc { get; set; }
            [XmlElement(ElementName = "fs9")]
            public Fs9 Fs9 { get; set; }
            [XmlElement(ElementName = "mfs")]
            public Mfs Mfs { get; set; }
            [XmlElement(ElementName = "fsl")]
            public Fsl Fsl { get; set; }
            [XmlElement(ElementName = "fsx")]
            public Fsx Fsx { get; set; }
            [XmlElement(ElementName = "fsn")]
            public Fsn Fsn { get; set; }
            [XmlElement(ElementName = "kml")]
            public Kml Kml { get; set; }
            [XmlElement(ElementName = "ify")]
            public Ify Ify { get; set; }
            [XmlElement(ElementName = "i74")]
            public I74 I74 { get; set; }
            [XmlElement(ElementName = "ifa")]
            public Ifa Ifa { get; set; }
            [XmlElement(ElementName = "inb")]
            public Inb Inb { get; set; }
            [XmlElement(ElementName = "ivo")]
            public Ivo Ivo { get; set; }
            [XmlElement(ElementName = "xvd")]
            public Xvd Xvd { get; set; }
            [XmlElement(ElementName = "xvp")]
            public Xvp Xvp { get; set; }
            [XmlElement(ElementName = "ixg")]
            public Ixg Ixg { get; set; }
            [XmlElement(ElementName = "jar")]
            public Jar Jar { get; set; }
            [XmlElement(ElementName = "jhe")]
            public Jhe Jhe { get; set; }
            [XmlElement(ElementName = "mdr")]
            public Mdr Mdr { get; set; }
            [XmlElement(ElementName = "mda")]
            public Mda Mda { get; set; }
            [XmlElement(ElementName = "lvd")]
            public Lvd Lvd { get; set; }
            [XmlElement(ElementName = "mjc")]
            public Mjc Mjc { get; set; }
            [XmlElement(ElementName = "mvz")]
            public Mvz Mvz { get; set; }
            [XmlElement(ElementName = "vms")]
            public Vms Vms { get; set; }
            [XmlElement(ElementName = "pmo")]
            public Pmo Pmo { get; set; }
            [XmlElement(ElementName = "pmr")]
            public Pmr Pmr { get; set; }
            [XmlElement(ElementName = "pmw")]
            public Pmw Pmw { get; set; }
            [XmlElement(ElementName = "pgt")]
            public Pgt Pgt { get; set; }
            [XmlElement(ElementName = "mga")]
            public Mga Mga { get; set; }
            [XmlElement(ElementName = "psm")]
            public Psm Psm { get; set; }
            [XmlElement(ElementName = "qty")]
            public Qty Qty { get; set; }
            [XmlElement(ElementName = "rmd")]
            public Rmd Rmd { get; set; }
            [XmlElement(ElementName = "sbr")]
            public Sbr Sbr { get; set; }
            [XmlElement(ElementName = "sfp")]
            public Sfp Sfp { get; set; }
            [XmlElement(ElementName = "tdg")]
            public Tdg Tdg { get; set; }
            [XmlElement(ElementName = "tfd")]
            public Tfd Tfd { get; set; }
            [XmlElement(ElementName = "ufc")]
            public Ufc Ufc { get; set; }
            [XmlElement(ElementName = "vas")]
            public Vas Vas { get; set; }
            [XmlElement(ElementName = "vfp")]
            public Vfp Vfp { get; set; }
            [XmlElement(ElementName = "wae")]
            public Wae Wae { get; set; }
            [XmlElement(ElementName = "xfm")]
            public Xfm Xfm { get; set; }
            [XmlElement(ElementName = "xpe")]
            public Xpe Xpe { get; set; }
            [XmlElement(ElementName = "xp9")]
            public Xp9 Xp9 { get; set; }
        }

        [XmlRoot(ElementName = "map")]
        public class Map
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
        }

        [XmlRoot(ElementName = "images")]
        public class Images
        {
            [XmlElement(ElementName = "directory")]
            public string Directory { get; set; }
            [XmlElement(ElementName = "map")]
            public List<Map> Map { get; set; }
        }

        [XmlRoot(ElementName = "links")]
        public class Links
        {
            [XmlElement(ElementName = "skyvector")]
            public string Skyvector { get; set; }
        }

        [XmlRoot(ElementName = "vatsim")]
        public class Vatsim
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "site")]
            public string Site { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
            [XmlElement(ElementName = "form")]
            public string Form { get; set; }
        }

        [XmlRoot(ElementName = "ivao")]
        public class Ivao
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "site")]
            public string Site { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
            [XmlElement(ElementName = "form")]
            public string Form { get; set; }
        }

        [XmlRoot(ElementName = "pilotedge")]
        public class Pilotedge
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "site")]
            public string Site { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
            [XmlElement(ElementName = "form")]
            public string Form { get; set; }
        }

        [XmlRoot(ElementName = "poscon")]
        public class Poscon
        {
            [XmlElement(ElementName = "name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "site")]
            public string Site { get; set; }
            [XmlElement(ElementName = "link")]
            public string Link { get; set; }
            [XmlElement(ElementName = "form")]
            public string Form { get; set; }
        }

        [XmlRoot(ElementName = "prefile")]
        public class Prefile
        {
            [XmlElement(ElementName = "vatsim")]
            public Vatsim Vatsim { get; set; }
            [XmlElement(ElementName = "ivao")]
            public Ivao Ivao { get; set; }
            [XmlElement(ElementName = "pilotedge")]
            public Pilotedge Pilotedge { get; set; }
            [XmlElement(ElementName = "poscon")]
            public Poscon Poscon { get; set; }
        }

        [XmlRoot(ElementName = "api_params")]
        public class Api_params
        {
            [XmlElement(ElementName = "airline")]
            public string Airline { get; set; }
            [XmlElement(ElementName = "fltnum")]
            public string Fltnum { get; set; }
            [XmlElement(ElementName = "type")]
            public string Type { get; set; }
            [XmlElement(ElementName = "orig")]
            public string Orig { get; set; }
            [XmlElement(ElementName = "dest")]
            public string Dest { get; set; }
            [XmlElement(ElementName = "date")]
            public string Date { get; set; }
            [XmlElement(ElementName = "dephour")]
            public string Dephour { get; set; }
            [XmlElement(ElementName = "depmin")]
            public string Depmin { get; set; }
            [XmlElement(ElementName = "route")]
            public string Route { get; set; }
            [XmlElement(ElementName = "stehour")]
            public string Stehour { get; set; }
            [XmlElement(ElementName = "stemin")]
            public string Stemin { get; set; }
            [XmlElement(ElementName = "reg")]
            public string Reg { get; set; }
            [XmlElement(ElementName = "fin")]
            public string Fin { get; set; }
            [XmlElement(ElementName = "selcal")]
            public string Selcal { get; set; }
            [XmlElement(ElementName = "pax")]
            public string Pax { get; set; }
            [XmlElement(ElementName = "altn")]
            public string Altn { get; set; }
            [XmlElement(ElementName = "fl")]
            public string Fl { get; set; }
            [XmlElement(ElementName = "cpt")]
            public string Cpt { get; set; }
            [XmlElement(ElementName = "pid")]
            public string Pid { get; set; }
            [XmlElement(ElementName = "fuelfactor")]
            public string Fuelfactor { get; set; }
            [XmlElement(ElementName = "manualzfw")]
            public string Manualzfw { get; set; }
            [XmlElement(ElementName = "addedfuel")]
            public string Addedfuel { get; set; }
            [XmlElement(ElementName = "contpct")]
            public string Contpct { get; set; }
            [XmlElement(ElementName = "resvrule")]
            public string Resvrule { get; set; }
            [XmlElement(ElementName = "taxiout")]
            public string Taxiout { get; set; }
            [XmlElement(ElementName = "taxiin")]
            public string Taxiin { get; set; }
            [XmlElement(ElementName = "cargo")]
            public string Cargo { get; set; }
            [XmlElement(ElementName = "origrwy")]
            public string Origrwy { get; set; }
            [XmlElement(ElementName = "destrwy")]
            public string Destrwy { get; set; }
            [XmlElement(ElementName = "climb")]
            public string Climb { get; set; }
            [XmlElement(ElementName = "descent")]
            public string Descent { get; set; }
            [XmlElement(ElementName = "cruisemode")]
            public string Cruisemode { get; set; }
            [XmlElement(ElementName = "cruisesub")]
            public string Cruisesub { get; set; }
            [XmlElement(ElementName = "planformat")]
            public string Planformat { get; set; }
            [XmlElement(ElementName = "pounds")]
            public string Pounds { get; set; }
            [XmlElement(ElementName = "navlog")]
            public string Navlog { get; set; }
            [XmlElement(ElementName = "etops")]
            public string Etops { get; set; }
            [XmlElement(ElementName = "stepclimbs")]
            public string Stepclimbs { get; set; }
            [XmlElement(ElementName = "tlr")]
            public string Tlr { get; set; }
            [XmlElement(ElementName = "notams_opt")]
            public string Notams_opt { get; set; }
            [XmlElement(ElementName = "firnot")]
            public string Firnot { get; set; }
            [XmlElement(ElementName = "maps")]
            public string Maps { get; set; }
            [XmlElement(ElementName = "turntoflt")]
            public string Turntoflt { get; set; }
            [XmlElement(ElementName = "turntoapt")]
            public string Turntoapt { get; set; }
            [XmlElement(ElementName = "turntotime")]
            public string Turntotime { get; set; }
            [XmlElement(ElementName = "turnfrflt")]
            public string Turnfrflt { get; set; }
            [XmlElement(ElementName = "turnfrapt")]
            public string Turnfrapt { get; set; }
            [XmlElement(ElementName = "turnfrtime")]
            public string Turnfrtime { get; set; }
            [XmlElement(ElementName = "fuelstats")]
            public string Fuelstats { get; set; }
            [XmlElement(ElementName = "contlabel")]
            public string Contlabel { get; set; }
            [XmlElement(ElementName = "static_id")]
            public string Static_id { get; set; }
            [XmlElement(ElementName = "acdata")]
            public string Acdata { get; set; }
            [XmlElement(ElementName = "acdata_parsed")]
            public string Acdata_parsed { get; set; }
        }

        [XmlRoot(ElementName = "OFP")]
        public class OFP
        {
            [XmlElement(ElementName = "fetch")]
            public Fetch Fetch { get; set; }
            [XmlElement(ElementName = "params")]
            public Params Params { get; set; }
            [XmlElement(ElementName = "general")]
            public General General { get; set; }
            [XmlElement(ElementName = "origin")]
            public Origin Origin { get; set; }
            [XmlElement(ElementName = "destination")]
            public Destination Destination { get; set; }
            [XmlElement(ElementName = "alternate")]
            public Alternate Alternate { get; set; }
            [XmlElement(ElementName = "takeoff_altn")]
            public string Takeoff_altn { get; set; }
            [XmlElement(ElementName = "enroute_altn")]
            public string Enroute_altn { get; set; }
            [XmlElement(ElementName = "navlog")]
            public Navlog Navlog { get; set; }
            [XmlElement(ElementName = "etops")]
            public string Etops { get; set; }
            [XmlElement(ElementName = "atc")]
            public Atc Atc { get; set; }
            [XmlElement(ElementName = "aircraft")]
            public Aircraft Aircraft { get; set; }
            [XmlElement(ElementName = "fuel")]
            public Fuel Fuel { get; set; }
            [XmlElement(ElementName = "times")]
            public Times Times { get; set; }
            [XmlElement(ElementName = "weights")]
            public Weights Weights { get; set; }
            [XmlElement(ElementName = "impacts")]
            public Impacts Impacts { get; set; }
            [XmlElement(ElementName = "crew")]
            public Crew Crew { get; set; }
            [XmlElement(ElementName = "notams")]
            public Notams Notams { get; set; }
            [XmlElement(ElementName = "weather")]
            public Weather Weather { get; set; }
            [XmlElement(ElementName = "sigmets")]
            public string Sigmets { get; set; }
            [XmlElement(ElementName = "text")]
            public Text Text { get; set; }
            [XmlElement(ElementName = "tracks")]
            public string Tracks { get; set; }
            [XmlElement(ElementName = "database_updates")]
            public Database_updates Database_updates { get; set; }
            [XmlElement(ElementName = "files")]
            public Files Files { get; set; }
            [XmlElement(ElementName = "fms_downloads")]
            public Fms_downloads Fms_downloads { get; set; }
            [XmlElement(ElementName = "images")]
            public Images Images { get; set; }
            [XmlElement(ElementName = "links")]
            public Links Links { get; set; }
            [XmlElement(ElementName = "prefile")]
            public Prefile Prefile { get; set; }
            [XmlElement(ElementName = "vatsim_prefile")]
            public string Vatsim_prefile { get; set; }
            [XmlElement(ElementName = "ivao_prefile")]
            public string Ivao_prefile { get; set; }
            [XmlElement(ElementName = "pilotedge_prefile")]
            public string Pilotedge_prefile { get; set; }
            [XmlElement(ElementName = "poscon_prefile")]
            public string Poscon_prefile { get; set; }
            [XmlElement(ElementName = "map_data")]
            public string Map_data { get; set; }
            [XmlElement(ElementName = "api_params")]
            public Api_params Api_params { get; set; }
        }
    }
}
