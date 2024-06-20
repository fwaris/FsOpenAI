module FsOpenAI.TravelSurvey.Loader
open System
open FsOpenAI.TravelSurvey.Types

let toCDIVMSAR (v:string) : CDIVMSAR =
    match int v with
    | 11 -> ``CDIVMSAR_New England MSACMSA of 1 million  with heavy rail``
    | 12 -> ``CDIVMSAR_New England MSACMSA of 1 million  wo heavy rail``
    | 13 -> ``CDIVMSAR_New England MSA of less than 1 million``
    | 14 -> ``CDIVMSAR_New England Not in an MSA``
    | 21 -> ``CDIVMSAR_Mid-Atlantic MSACMSA of 1 million  with heavy rail``
    | 22 -> ``CDIVMSAR_Mid-Atlantic MSACMSA of 1 million  wo heavy rail``
    | 23 -> ``CDIVMSAR_Mid-Atlantic MSA of less than 1 million``
    | 24 -> ``CDIVMSAR_Mid-Atlantic Not in an MSA``
    | 31 -> ``CDIVMSAR_East North Central MSACMSA of 1 million  with heavy rail``
    | 32 -> ``CDIVMSAR_East North Central MSACMSA of 1 million  wo heavy rail``
    | 33 -> ``CDIVMSAR_East North Central MSA of less than 1 million``
    | 34 -> ``CDIVMSAR_East North Central Not in an MSA``
    | 42 -> ``CDIVMSAR_West North Central MSACMSA of 1 million  wo heavy rail``
    | 43 -> ``CDIVMSAR_West North Central MSA of less than 1 million``
    | 44 -> ``CDIVMSAR_West North Central Not in an MSA``
    | 51 -> ``CDIVMSAR_South Atlantic MSACMSA of 1 million  with heavy rail``
    | 52 -> ``CDIVMSAR_South Atlantic MSACMSA of 1 million  wo heavy rail``
    | 53 -> ``CDIVMSAR_South Atlantic MSA of less than 1 million``
    | 54 -> ``CDIVMSAR_South Atlantic Not in an MSA``
    | 62 -> ``CDIVMSAR_East South Central MSACMSA of 1 million  wo heavy rail``
    | 63 -> ``CDIVMSAR_East South Central MSA of less than 1 million``
    | 64 -> ``CDIVMSAR_East South Central Not in an MSA``
    | 72 -> ``CDIVMSAR_West South Central MSACMSA of 1 million  wo heavy rail``
    | 73 -> ``CDIVMSAR_West South Central MSA of less than 1 million``
    | 74 -> ``CDIVMSAR_West South Central Not in an MSA``
    | 82 -> ``CDIVMSAR_Mountain MSACMSA of 1 million  wo heavy rail``
    | 83 -> ``CDIVMSAR_Mountain MSA of less than 1 million``
    | 84 -> ``CDIVMSAR_Mountain Not in an MSA``
    | 91 -> ``CDIVMSAR_Pacific MSACMSA of 1 million  with heavy rail``
    | 92 -> ``CDIVMSAR_Pacific MSACMSA of 1 million  wo heavy rail``
    | 93 -> ``CDIVMSAR_Pacific MSA of less than 1 million``
    | 94 -> ``CDIVMSAR_Pacific Not in an MSA``

let toCENSUS_D (v:string) : CENSUS_D =
    match int v with
    | 1 -> ``CENSUS_D_New England``
    | 2 -> ``CENSUS_D_Middle Atlantic``
    | 3 -> ``CENSUS_D_East North Central``
    | 4 -> ``CENSUS_D_West North Central``
    | 5 -> ``CENSUS_D_South Atlantic``
    | 6 -> ``CENSUS_D_East South Central``
    | 7 -> ``CENSUS_D_West South Central``
    | 8 -> CENSUS_D_Mountain
    | 9 -> CENSUS_D_Pacific

let toCENSUS_R (v:string) : CENSUS_R =
    match int v with
    | 1 -> CENSUS_R_Northeast
    | 2 -> CENSUS_R_Midwest
    | 3 -> CENSUS_R_South
    | 4 -> CENSUS_R_West

let toFLAG100 (v:string) : FLAG100 =
    match int v with
    | 1 -> ``FLAG100_All eligible household members completed``
    | 2 -> ``FLAG100_75% to 99% of eligible household members completed``

let toHHFAMINC (v:string) : HHFAMINC =
    match int v with
    | -7 -> ``HHFAMINC_I prefer not to answer``
    | -8 -> ``HHFAMINC_I don't know``
    | 1 -> ``HHFAMINC_Less than 10000``
    | 2 -> ``HHFAMINC_10000 to 14999``
    | 3 -> ``HHFAMINC_15000 to 24999``
    | 4 -> ``HHFAMINC_25000 to 34999``
    | 5 -> ``HHFAMINC_35000 to 49999``
    | 6 -> ``HHFAMINC_50000 to 74999``
    | 7 -> ``HHFAMINC_75000 to 99999``
    | 8 -> ``HHFAMINC_100000 to 124999``
    | 9 -> ``HHFAMINC_125000 to 149999``
    | 10 -> ``HHFAMINC_150000 to 199999``
    | 11 -> ``HHFAMINC_200000 or more``

let toHHFAMINC_IMP (v:string) : HHFAMINC_IMP =
    match int v with
    | 1 -> ``HHFAMINC_IMP_Less than 10000``
    | 2 -> ``HHFAMINC_IMP_10000 to 14999``
    | 3 -> ``HHFAMINC_IMP_15000 to 24999``
    | 4 -> ``HHFAMINC_IMP_25000 to 34999``
    | 5 -> ``HHFAMINC_IMP_35000 to 49999``
    | 6 -> ``HHFAMINC_IMP_50000 to 74999``
    | 7 -> ``HHFAMINC_IMP_75000 to 99999``
    | 8 -> ``HHFAMINC_IMP_100000 to 124999``
    | 9 -> ``HHFAMINC_IMP_125000 to 149999``
    | 10 -> ``HHFAMINC_IMP_150000 to 199999``
    | 11 -> ``HHFAMINC_IMP_200000 or more``

let toHHRELATD (v:string) : HHRELATD =
    match int v with
    | 1 -> ``HHRELATD_At least two persons in hh are related``
    | 2 -> ``HHRELATD_No related persons in hh``

let toHH_HISP (v:string) : HH_HISP =
    match int v with
    | 1 -> ``HH_HISP_Hispanic or Latino``
    | 2 -> ``HH_HISP_Not Hispanic or Latino``

let toHH_RACE (v:string) : HH_RACE =
    match int v with
    | 1 -> HH_RACE_White
    | 2 -> ``HH_RACE_Black or African American``
    | 3 -> HH_RACE_Asian
    | 4 -> ``HH_RACE_American IndianAlaska Native``
    | 5 -> ``HH_RACE_Native HawaiianPacific Islander``
    | 6 -> ``HH_RACE_Multiple races selected``
    | 97 -> ``HH_RACE_Other race``

let toHOMEOWN (v:string) : HOMEOWN =
    match int v with
    | 1 -> ``HOMEOWN_Owned by hh member with mortgage or loan``
    | 2 -> ``HOMEOWN_Owned by hh member free and clear (no mortgage)``
    | 3 -> ``HOMEOWN_Rented by hh member``
    | 4 -> ``HOMEOWN_Occupied without payment``

let toHOMETYPE (v:string) : HOMETYPE =
    match int v with
    | 1 -> ``HOMETYPE_One-family detached``
    | 2 -> ``HOMETYPE_One-family attached (townhome condo)``
    | 3 -> ``HOMETYPE_Building with 2 or more apartments``
    | 4 -> ``HOMETYPE_Mobile home``
    | 5 -> ``HOMETYPE_Boat RV van etc``

let toLIF_CYC (v:string) : LIF_CYC =
    match int v with
    | 1 -> ``LIF_CYC_one adult no children``
    | 2 -> ``LIF_CYC_2  adults no children``
    | 3 -> ``LIF_CYC_one adult youngest child 0-5``
    | 4 -> ``LIF_CYC_2  adults youngest child 0-5``
    | 5 -> ``LIF_CYC_one adult youngest child 6-15``
    | 6 -> ``LIF_CYC_2  adults youngest child 6-15``
    | 7 -> ``LIF_CYC_one adult youngest child 16-21``
    | 8 -> ``LIF_CYC_2  adults youngest child 16-21``
    | 9 -> ``LIF_CYC_one adult retired no children``
    | 10 -> ``LIF_CYC_2  adults retired no children``

let toMSACAT (v:string) : MSACAT =
    match int v with
    | 1 -> ``MSACAT_MSA of 1 million or more with rail``
    | 2 -> ``MSACAT_MSA of 1 million or more without rail``
    | 3 -> ``MSACAT_MSA less than 1 million``
    | 4 -> ``MSACAT_Not in MSA``

let toMSASIZE (v:string) : MSASIZE =
    match int v with
    | 1 -> ``MSASIZE_In an MSA of Less than 250000``
    | 2 -> ``MSASIZE_In an MSA of 250000 - 499999``
    | 3 -> ``MSASIZE_In an MSA of 500000 - 999999``
    | 4 -> ``MSASIZE_In an MSA or CMSA of 1000000 - 2999999``
    | 5 -> ``MSASIZE_In an MSA or CMSA of 3 million or more``
    | 6 -> ``MSASIZE_Not in MSA or CMSA``

let toTRAVDAY (v:string) : TRAVDAY =
    match int v with
    | 1 -> TRAVDAY_Sunday
    | 2 -> TRAVDAY_Monday
    | 3 -> TRAVDAY_Tuesday
    | 4 -> TRAVDAY_Wednesday
    | 5 -> TRAVDAY_Thursday
    | 6 -> TRAVDAY_Friday
    | 7 -> TRAVDAY_Saturday

let toURBAN (v:string) : URBAN =
    match int v with
    | 1 -> ``URBAN_In an urban area``
    | 2 -> ``URBAN_In an Urban cluster``
    | 3 -> ``URBAN_In an area surrounded by urban areas``
    | 4 -> ``URBAN_Not in urban area``

let toURBANSIZE (v:string) : URBANSIZE =
    match int v with
    | 1 -> ``URBANSIZE_50000-199999``
    | 2 -> ``URBANSIZE_200000-499999``
    | 3 -> ``URBANSIZE_500000-999999``
    | 4 -> ``URBANSIZE_1000000 or more with heavy rail``
    | 5 -> ``URBANSIZE_1000000 or more without heavy rail``
    | 6 -> ``URBANSIZE_Not in urbanized area``

let toURBRUR (v:string) : URBRUR =
    match int v with
    | 1 -> URBRUR_Urban
    | 2 -> URBRUR_Rural

let toMAKE (v:string) : MAKE =
    match int v with
    | 0 -> ``MAKE_Suppressed for confidential reason``
    | 2 -> MAKE_Jeep
    | 6 -> MAKE_Chrysler
    | 7 -> MAKE_Dodge
    | 12 -> MAKE_Ford
    | 13 -> MAKE_Lincoln
    | 18 -> ``MAKE_Buick  Opel``
    | 19 -> MAKE_Cadillac
    | 20 -> MAKE_Chevrolet
    | 22 -> MAKE_Pontiac
    | 23 -> MAKE_GMC
    | 29 -> ``MAKE_Other Domestic Manufacturers (eg Tesla)``
    | 30 -> MAKE_Volkswagen
    | 32 -> MAKE_Audi
    | 34 -> MAKE_BMW
    | 35 -> ``MAKE_NissanDatsun``
    | 37 -> MAKE_Honda
    | 41 -> MAKE_Mazda
    | 42 -> ``MAKE_Mercedes-Benz``
    | 48 -> MAKE_Subaru
    | 49 -> MAKE_Toyota
    | 51 -> MAKE_Volvo
    | 52 -> MAKE_Mitsubishi
    | 54 -> MAKE_Acura
    | 55 -> MAKE_Hyundai
    | 58 -> MAKE_Infiniti
    | 59 -> MAKE_Lexus
    | 63 -> MAKE_KIA
    | 72 -> ``MAKE_Harley-Davidson``
    | 98 -> ``MAKE_Other Make``

let toVEHTYPE (v:string) : VEHTYPE =
    match int v with
    | 1 -> ``VEHTYPE_AutomobileCarStationwagon``
    | 2 -> ``VEHTYPE_Van (MinivanCargoPassenger)``
    | 3 -> ``VEHTYPE_SUV (Santa Fe Tahoe Jeep etc)``
    | 4 -> ``VEHTYPE_Pickup Truck``
    | 5 -> ``VEHTYPE_Other Truck``
    | 6 -> ``VEHTYPE_Recreational vehicle (RV)Motorhome``
    | 7 -> VEHTYPE_MotorcycleMoped
    | 97 -> ``VEHTYPE_Something else``

let toPROXY (v:string) : PROXY =
    match int v with
    | 1 -> PROXY_Self
    | 2 -> ``PROXY_Someone else``

let toR_HISP (v:string) : R_HISP =
    match int v with
    | 1 -> R_HISP_Hispanic
    | 2 -> ``R_HISP_Not Hispanic``

let toR_SEX_IMP (v:string) : R_SEX_IMP =
    match int v with
    | 1 -> R_SEX_IMP_Male
    | 2 -> R_SEX_IMP_Female

let toLOOP_TRIP (v:string) : LOOP_TRIP =
    match int v with
    | 1 -> ``LOOP_TRIP_Loop trip``
    | 2 -> ``LOOP_TRIP_Not a loop trip``

let toONTD_P1 (v:string) : ONTD_P1 =
    match int v with
    | 1 -> ONTD_P1_Selected
    | 2 -> ``ONTD_P1_Not Selected``

let toPUBTRANS (v:string) : PUBTRANS =
    match int v with
    | 1 -> ``PUBTRANS_Used public transit``
    | 2 -> ``PUBTRANS_Did not use public transit``

let toR_SEX (v:string) : R_SEX =
    match int v with
    | -7 -> R_SEX_Refuse
    | -8 -> ``R_SEX_Don't know``
    | 1 -> R_SEX_Male
    | 2 -> R_SEX_Female

let toTDWKND (v:string) : TDWKND =
    match int v with
    | 1 -> TDWKND_Weekend
    | 2 -> TDWKND_Weekday

let toTRPTRANS (v:string) : TRPTRANS =
    match int v with
    | 1 -> TRPTRANS_Car
    | 2 -> TRPTRANS_Van
    | 3 -> ``TRPTRANS_SUVCrossover``
    | 4 -> ``TRPTRANS_Pickup truck``
    | 6 -> ``TRPTRANS_Recreational Vehicle``
    | 7 -> TRPTRANS_Motorcycle
    | 8 -> ``TRPTRANS_Public or commuter bus``
    | 9 -> ``TRPTRANS_School bus``
    | 10 -> ``TRPTRANS_Street car or trolley car``
    | 11 -> ``TRPTRANS_Subway or elevated rail``
    | 12 -> ``TRPTRANS_Commuter rail``
    | 13 -> TRPTRANS_Amtrak
    | 14 -> TRPTRANS_Airplane
    | 15 -> ``TRPTRANS_Taxicab or limo service``
    | 16 -> ``TRPTRANS_Other ride-sharing service``
    | 17 -> ``TRPTRANS_Paratransit Dial a ride``
    | 18 -> ``TRPTRANS_Bicycle (including bikeshare ebike etc)``
    | 19 -> ``TRPTRANS_E-scooter``
    | 20 -> TRPTRANS_Walked
    | 21 -> ``TRPTRANS_Other (specify)``

let toWHYTO (v:string) : WHYTO =
    match int v with
    | 1 -> ``WHYTO_Regular activities at home``
    | 2 -> ``WHYTO_Work from home (paid)``
    | 3 -> ``WHYTO_Work at a non-home location``
    | 4 -> ``WHYTO_Work activity to drop-offpickup someonesomething``
    | 5 -> ``WHYTO_Other work-related activities``
    | 6 -> ``WHYTO_Attend school as a student``
    | 7 -> ``WHYTO_Attend childcare or adult care``
    | 8 -> ``WHYTO_Volunteer activities (not paid)``
    | 9 -> ``WHYTO_Change type of transportation``
    | 10 -> ``WHYTO_Drop offpick up someone (personal)``
    | 11 -> ``WHYTO_Health care visit``
    | 12 -> ``WHYTO_Buy meals``
    | 13 -> ``WHYTO_Shopbuypick-up or return goods``
    | 14 -> ``WHYTO_Other familypersonal errands``
    | 15 -> ``WHYTO_Recreational activities``
    | 16 -> WHYTO_Exercise
    | 17 -> ``WHYTO_Visit friends or relatives``
    | 18 -> ``WHYTO_Rest or relaxationvacation``
    | 19 -> ``WHYTO_Religious or other community activities``
    | 97 -> ``WHYTO_Something else (specify)``

let toWHYTRP1S (v:string) : WHYTRP1S =
    match int v with
    | 1 -> WHYTRP1S_Home
    | 10 -> WHYTRP1S_Work
    | 20 -> ``WHYTRP1S_SchoolDaycareReligious``
    | 30 -> ``WHYTRP1S_MedicalDental services``
    | 40 -> ``WHYTRP1S_ShoppingErrands``
    | 50 -> ``WHYTRP1S_SocialRecreational``
    | 70 -> ``WHYTRP1S_Transport someone``
    | 80 -> WHYTRP1S_Meals
    | 97 -> ``WHYTRP1S_Something else``

let toWHYTRP90 (v:string) : WHYTRP90 =
    match int v with
    | 1 -> ``WHYTRP90_ToFrom Work``
    | 2 -> ``WHYTRP90_Work-Related Business``
    | 3 -> WHYTRP90_Shopping
    | 4 -> ``WHYTRP90_Other FamilyPersonal Business``
    | 5 -> ``WHYTRP90_SchoolChurch``
    | 6 -> ``WHYTRP90_MedicalDental``
    | 8 -> ``WHYTRP90_Visit FriendsRelatives``
    | 10 -> ``WHYTRP90_Other SocialRecreational``
    | 11 -> WHYTRP90_Other
    | 99 -> ``WHYTRP90_Refused  Don't Know``


let toBaseResponse (v:string) : Response = 
    match int v with
    | -1 -> R_NotAscertained
    | -9 -> R_Skipped
    | x -> Value(float x)


let toYesNo (v:string) : YesNo = 
    match int v with
    | -1 -> YN_NotAscertained
    | -9 -> YN_Skipped
    | 1 -> Yes
    | 2 -> No


let toFloat (v:string) : float = 
    match Double.TryParse v with
    | true, f -> f
    | _ -> 0.0


let toDateTime (v:string) : DateTime = 
    match DateTime.TryParseExact(v, "yyyyMM", null, System.Globalization.DateTimeStyles.None) with
    | true, d -> d
    | _ -> DateTime.MinValue

let to_Household (vals:string[]) =
 {
        HOUSEID = string vals.[0]
        WTHHFIN = toFloat vals.[1]
        WTHHFIN5D = toFloat vals.[2]
        WTHHFIN2D = toFloat vals.[3]
        NUMADLT = toFloat vals.[4]
        HOMEOWN = toHOMEOWN vals.[5]
        HOMETYPE = toHOMETYPE vals.[6]
        RAIL = toYesNo vals.[7]
        CENSUS_D = toCENSUS_D vals.[8]
        CENSUS_R = toCENSUS_R vals.[9]
        HH_HISP = toHH_HISP vals.[10]
        DRVRCNT = toFloat vals.[11]
        CNTTDHH = toFloat vals.[12]
        CDIVMSAR = toCDIVMSAR vals.[13]
        FLAG100 = toFLAG100 vals.[14]
        HHFAMINC = toHHFAMINC vals.[15]
        HHFAMINC_IMP = toHHFAMINC_IMP vals.[16]
        HH_RACE = toHH_RACE vals.[17]
        HHSIZE = toFloat vals.[18]
        HHVEHCNT = toFloat vals.[19]
        HHRELATD = toHHRELATD vals.[20]
        LIF_CYC = toLIF_CYC vals.[21]
        MSACAT = toMSACAT vals.[22]
        MSASIZE = toMSASIZE vals.[23]
        TRAVDAY = toTRAVDAY vals.[24]
        URBAN = toURBAN vals.[25]
        URBANSIZE = toURBANSIZE vals.[26]
        URBRUR = toURBRUR vals.[27]
        PPT517 = toFloat vals.[28]
        YOUNGCHILD = toFloat vals.[29]
        RESP_CNT = toFloat vals.[30]
        URBRUR_2010 = toURBRUR vals.[31]
        TDAYDATE = toDateTime vals.[32]
        WRKCOUNT = toFloat vals.[33]
        STRATUMID = string vals.[34]
}

let to_Vehicle (vals:string[]) =
 {
        HOUSEID = string vals.[0]
        VEHID = string vals.[1]
        VEHYEAR = toFloat vals.[2]
        MAKE = toMAKE vals.[3]
        HHVEHCNT = toFloat vals.[4]
        VEHTYPE = toBaseResponse vals.[5]
        VEHFUEL = toBaseResponse vals.[6]
        VEHCOMMERCIAL = toBaseResponse vals.[7]
        VEHCOM_RS = toBaseResponse vals.[8]
        VEHCOM_DEL = toBaseResponse vals.[9]
        VEHCOM_OTH = toBaseResponse vals.[10]
        COMMERCIALFREQ = toBaseResponse vals.[11]
        HHVEHUSETIME_RS = toBaseResponse vals.[12]
        HHVEHUSETIME_DEL = toBaseResponse vals.[13]
        HHVEHUSETIME_OTH = toBaseResponse vals.[14]
        VEHOWNED = toBaseResponse vals.[15]
        WHOMAIN = toBaseResponse vals.[16]
        VEHCASEID = toBaseResponse vals.[17]
        ANNMILES = toBaseResponse vals.[18]
        HYBRID = toYesNo vals.[19]
        VEHAGE = toFloat vals.[20]
        VEHOWNMO = toBaseResponse vals.[21]
        NUMADLT = toFloat vals.[22]
        HOMEOWN = toHOMEOWN vals.[23]
        RAIL = toYesNo vals.[24]
        CENSUS_D = toCENSUS_D vals.[25]
        CENSUS_R = toCENSUS_R vals.[26]
        HH_HISP = toHH_HISP vals.[27]
        DRVRCNT = toFloat vals.[28]
        CDIVMSAR = toCDIVMSAR vals.[29]
        HHFAMINC = toHHFAMINC vals.[30]
        HH_RACE = toHH_RACE vals.[31]
        HHSIZE = toFloat vals.[32]
        LIF_CYC = toLIF_CYC vals.[33]
        MSACAT = toMSACAT vals.[34]
        MSASIZE = toMSASIZE vals.[35]
        TRAVDAY = toTRAVDAY vals.[36]
        URBAN = toURBAN vals.[37]
        URBANSIZE = toURBANSIZE vals.[38]
        URBRUR = toURBRUR vals.[39]
        TDAYDATE = toDateTime vals.[40]
        WRKCOUNT = toFloat vals.[41]
        STRATUMID = string vals.[42]
        WTHHFIN = toFloat vals.[43]
        WTHHFIN5D = toFloat vals.[44]
        WTHHFIN2D = toFloat vals.[45]
        HHFAMINC_IMP = toHHFAMINC_IMP vals.[46]
}

let to_Person (vals:string[]) =
 {
        HOUSEID = string vals.[0]
        PERSONID = string vals.[1]
        WTPERFIN = toFloat vals.[2]
        WTPERFIN5D = toFloat vals.[3]
        WTPERFIN2D = toFloat vals.[4]
        R_AGE = toFloat vals.[5]
        R_SEX = toBaseResponse vals.[6]
        R_RELAT = toBaseResponse vals.[7]
        WORKER = toBaseResponse vals.[8]
        DRIVER = toBaseResponse vals.[9]
        R_RACE = toBaseResponse vals.[10]
        GCDWORK = toBaseResponse vals.[11]
        OUTOFTWN = toYesNo vals.[12]
        USEPUBTR = toYesNo vals.[13]
        R_RACE_IMP = toHH_RACE vals.[14]
        R_HISP = toR_HISP vals.[15]
        PROXY = toPROXY vals.[16]
        WHOPROXY = toBaseResponse vals.[17]
        EDUC = toBaseResponse vals.[18]
        LAST30_TAXI = toBaseResponse vals.[19]
        LAST30_RDSHR = toBaseResponse vals.[20]
        LAST30_ESCT = toBaseResponse vals.[21]
        LAST30_PT = toBaseResponse vals.[22]
        LAST30_MTRC = toBaseResponse vals.[23]
        LAST30_WALK = toBaseResponse vals.[24]
        LAST30_BIKE = toBaseResponse vals.[25]
        LAST30_BKSHR = toBaseResponse vals.[26]
        TAXISERVICE = toBaseResponse vals.[27]
        RIDESHARE22 = toBaseResponse vals.[28]
        ESCOOTERUSED = toBaseResponse vals.[29]
        PTUSED = toBaseResponse vals.[30]
        TRNPASS = toBaseResponse vals.[31]
        MCTRANSIT = toBaseResponse vals.[32]
        WALKTRANSIT = toBaseResponse vals.[33]
        BIKETRANSIT = toBaseResponse vals.[34]
        BIKESHARE22 = toBaseResponse vals.[35]
        USAGE1 = toBaseResponse vals.[36]
        USAGE2_1 = toBaseResponse vals.[37]
        USAGE2_2 = toBaseResponse vals.[38]
        USAGE2_3 = toBaseResponse vals.[39]
        USAGE2_4 = toBaseResponse vals.[40]
        USAGE2_5 = toBaseResponse vals.[41]
        USAGE2_6 = toBaseResponse vals.[42]
        USAGE2_7 = toBaseResponse vals.[43]
        USAGE2_8 = toBaseResponse vals.[44]
        USAGE2_9 = toBaseResponse vals.[45]
        USAGE2_10 = toBaseResponse vals.[46]
        QACSLAN1 = toBaseResponse vals.[47]
        QACSLAN3 = toBaseResponse vals.[48]
        PAYPROF = toBaseResponse vals.[49]
        PRMACT = toBaseResponse vals.[50]
        EMPLOYMENT2 = toBaseResponse vals.[51]
        DRIVINGOCCUPATION = toBaseResponse vals.[52]
        DRIVINGVEHICLE = toBaseResponse vals.[53]
        WRKLOC = toBaseResponse vals.[54]
        WKFMHM22 = toBaseResponse vals.[55]
        WRKTRANS = toBaseResponse vals.[56]
        EMPPASS = toBaseResponse vals.[57]
        SCHOOL1 = toBaseResponse vals.[58]
        STUDE = toBaseResponse vals.[59]
        SCHTYP = toBaseResponse vals.[60]
        SCHOOL1C = toBaseResponse vals.[61]
        SCHTRN1 = toBaseResponse vals.[62]
        DELIVER = toBaseResponse vals.[63]
        DELIV_GOOD = toBaseResponse vals.[64]
        DELIV_FOOD = toBaseResponse vals.[65]
        DELIV_GROC = toBaseResponse vals.[66]
        DELIV_PERS = toBaseResponse vals.[67]
        RET_HOME = toBaseResponse vals.[68]
        RET_PUF = toBaseResponse vals.[69]
        RET_AMZ = toBaseResponse vals.[70]
        RET_STORE = toBaseResponse vals.[71]
        MEDCOND = toBaseResponse vals.[72]
        MEDCOND6 = toBaseResponse vals.[73]
        W_CANE = toBaseResponse vals.[74]
        W_WKCR = toBaseResponse vals.[75]
        W_VISIMP = toBaseResponse vals.[76]
        W_SCCH = toBaseResponse vals.[77]
        W_CHAIR = toBaseResponse vals.[78]
        W_NONE = toBaseResponse vals.[79]
        CONDTRAV = toBaseResponse vals.[80]
        CONDRIDE = toBaseResponse vals.[81]
        CONDNIGH = toBaseResponse vals.[82]
        CONDRIVE = toBaseResponse vals.[83]
        CONDPUB = toBaseResponse vals.[84]
        CONDSPEC = toBaseResponse vals.[85]
        CONDSHARE = toBaseResponse vals.[86]
        CONDNONE = toBaseResponse vals.[87]
        CONDRF = toBaseResponse vals.[88]
        FRSTHM = toYesNo vals.[89]
        PARK = toBaseResponse vals.[90]
        PARKHOME = toBaseResponse vals.[91]
        PARKHOMEAMT = toBaseResponse vals.[92]
        PARKHOMEAMT_PAMOUNT = toBaseResponse vals.[93]
        PARKHOMEAMT_PAYTYPE = toBaseResponse vals.[94]
        SAMEPLC = toBaseResponse vals.[95]
        COV1_WK = toBaseResponse vals.[96]
        COV1_SCH = toBaseResponse vals.[97]
        COV1_PT = toBaseResponse vals.[98]
        COV1_OHD = toBaseResponse vals.[99]
        COV2_WK = toBaseResponse vals.[100]
        COV2_SCH = toBaseResponse vals.[101]
        COV2_PT = toBaseResponse vals.[102]
        COV2_OHD = toBaseResponse vals.[103]
        CNTTDTR = toFloat vals.[104]
        R_SEX_IMP = toR_SEX_IMP vals.[105]
        NUMADLT = toFloat vals.[106]
        HOMEOWN = toHOMEOWN vals.[107]
        RAIL = toYesNo vals.[108]
        CENSUS_D = toCENSUS_D vals.[109]
        CENSUS_R = toCENSUS_R vals.[110]
        HH_HISP = toHH_HISP vals.[111]
        DRVRCNT = toFloat vals.[112]
        CDIVMSAR = toCDIVMSAR vals.[113]
        HHFAMINC = toHHFAMINC vals.[114]
        HH_RACE = toHH_RACE vals.[115]
        HHSIZE = toFloat vals.[116]
        HHVEHCNT = toFloat vals.[117]
        LIF_CYC = toLIF_CYC vals.[118]
        MSACAT = toMSACAT vals.[119]
        MSASIZE = toMSASIZE vals.[120]
        TRAVDAY = toTRAVDAY vals.[121]
        URBAN = toURBAN vals.[122]
        URBANSIZE = toURBANSIZE vals.[123]
        URBRUR = toURBRUR vals.[124]
        TDAYDATE = toDateTime vals.[125]
        WRKCOUNT = toFloat vals.[126]
        STRATUMID = string vals.[127]
        HHFAMINC_IMP = toHHFAMINC_IMP vals.[128]
}

let to_Trip (vals:string[]) =
 {
        HOUSEID = string vals.[0]
        PERSONID = string vals.[1]
        TRIPID = string vals.[2]
        SEQ_TRIPID = string vals.[3]
        VEHCASEID = toBaseResponse vals.[4]
        FRSTHM = toYesNo vals.[5]
        PARK = toBaseResponse vals.[6]
        HHMEMDRV = toBaseResponse vals.[7]
        TDWKND = toTDWKND vals.[8]
        TRAVDAY = toTRAVDAY vals.[9]
        LOOP_TRIP = toLOOP_TRIP vals.[10]
        DWELTIME = toBaseResponse vals.[11]
        PUBTRANS = toPUBTRANS vals.[12]
        TRIPPURP = toBaseResponse vals.[13]
        WHYFROM = toBaseResponse vals.[14]
        WHYTRP1S = toWHYTRP1S vals.[15]
        TRVLCMIN = toBaseResponse vals.[16]
        STRTTIME = toBaseResponse vals.[17]
        ENDTIME = toBaseResponse vals.[18]
        TRPHHVEH = toBaseResponse vals.[19]
        VEHID = string vals.[20]
        TRPTRANS = toTRPTRANS vals.[21]
        NUMONTRP = toFloat vals.[22]
        ONTD_P1 = toONTD_P1 vals.[23]
        ONTD_P2 = toBaseResponse vals.[24]
        ONTD_P3 = toBaseResponse vals.[25]
        ONTD_P4 = toBaseResponse vals.[26]
        ONTD_P5 = toBaseResponse vals.[27]
        ONTD_P6 = toBaseResponse vals.[28]
        ONTD_P7 = toBaseResponse vals.[29]
        ONTD_P8 = toBaseResponse vals.[30]
        ONTD_P9 = toBaseResponse vals.[31]
        ONTD_P10 = toBaseResponse vals.[32]
        NONHHCNT = toFloat vals.[33]
        HHACCCNT = toFloat vals.[34]
        WHODROVE = toBaseResponse vals.[35]
        DRVR_FLG = toBaseResponse vals.[36]
        PSGR_FLG = toBaseResponse vals.[37]
        WHODROVE_IMP = toBaseResponse vals.[38]
        PARK2_PAMOUNT = toBaseResponse vals.[39]
        PARK2_PAYTYPE = toBaseResponse vals.[40]
        PARK2 = toBaseResponse vals.[41]
        WHYTO = toWHYTO vals.[42]
        WALK = toBaseResponse vals.[43]
        TRPMILES = toBaseResponse vals.[44]
        WTTRDFIN = toFloat vals.[45]
        WTTRDFIN5D = toFloat vals.[46]
        WTTRDFIN2D = toFloat vals.[47]
        TDCASEID = string vals.[48]
        VMT_MILE = toBaseResponse vals.[49]
        GASPRICE = toFloat vals.[50]
        WHYTRP90 = toWHYTRP90 vals.[51]
        NUMADLT = toFloat vals.[52]
        HOMEOWN = toHOMEOWN vals.[53]
        RAIL = toYesNo vals.[54]
        CENSUS_D = toCENSUS_D vals.[55]
        CENSUS_R = toCENSUS_R vals.[56]
        HH_HISP = toHH_HISP vals.[57]
        DRVRCNT = toFloat vals.[58]
        CDIVMSAR = toCDIVMSAR vals.[59]
        HHFAMINC = toHHFAMINC vals.[60]
        HH_RACE = toHH_RACE vals.[61]
        HHSIZE = toFloat vals.[62]
        HHVEHCNT = toFloat vals.[63]
        LIF_CYC = toLIF_CYC vals.[64]
        MSACAT = toMSACAT vals.[65]
        MSASIZE = toMSASIZE vals.[66]
        URBAN = toURBAN vals.[67]
        URBANSIZE = toURBANSIZE vals.[68]
        URBRUR = toURBRUR vals.[69]
        TDAYDATE = toDateTime vals.[70]
        WRKCOUNT = toFloat vals.[71]
        STRATUMID = string vals.[72]
        R_AGE = toFloat vals.[73]
        R_SEX = toBaseResponse vals.[74]
        WORKER = toBaseResponse vals.[75]
        DRIVER = toBaseResponse vals.[76]
        R_RACE = toBaseResponse vals.[77]
        R_HISP = toR_HISP vals.[78]
        PROXY = toPROXY vals.[79]
        EDUC = toBaseResponse vals.[80]
        PRMACT = toBaseResponse vals.[81]
        R_SEX_IMP = toR_SEX_IMP vals.[82]
        VEHTYPE = toBaseResponse vals.[83]
        HHFAMINC_IMP = toHHFAMINC_IMP vals.[84]
}

let to_LongTrip (vals:string[]) =
 {
        HOUSEID = string vals.[0]
        PERSONID = string vals.[1]
        LONGDIST = toFloat vals.[2]
        MAINMODE = toBaseResponse vals.[3]
        INT_FLAG = toBaseResponse vals.[4]
        LD_NUMONTRP = toBaseResponse vals.[5]
        ONTP_P1 = toBaseResponse vals.[6]
        ONTP_P2 = toBaseResponse vals.[7]
        ONTP_P3 = toBaseResponse vals.[8]
        ONTP_P4 = toBaseResponse vals.[9]
        ONTP_P5 = toBaseResponse vals.[10]
        ONTP_P6 = toBaseResponse vals.[11]
        ONTP_P7 = toBaseResponse vals.[12]
        ONTP_P8 = toBaseResponse vals.[13]
        ONTP_P9 = toBaseResponse vals.[14]
        ONTP_P10 = toBaseResponse vals.[15]
        FARREAS = toBaseResponse vals.[16]
        LD_AMT = toFloat vals.[17]
        LD_ICB = toFloat vals.[18]
        LDT_FLAG = toBaseResponse vals.[19]
        BEGTRIP = toBaseResponse vals.[20]
        ENDTRIP = toBaseResponse vals.[21]
        NTSAWAY = toBaseResponse vals.[22]
        WEEKEND = toBaseResponse vals.[23]
        MRT_DATE = toBaseResponse vals.[24]
        FARCDIV = toBaseResponse vals.[25]
        FARCREG = toBaseResponse vals.[26]
        GCDTOT = toBaseResponse vals.[27]
        AIRSIZE = toBaseResponse vals.[28]
        EXITCDIV = toBaseResponse vals.[29]
        GCD_FLAG = toBaseResponse vals.[30]
        NUMADLT = toFloat vals.[31]
        HOMEOWN = toHOMEOWN vals.[32]
        RAIL = toYesNo vals.[33]
        CENSUS_D = toCENSUS_D vals.[34]
        CENSUS_R = toCENSUS_R vals.[35]
        HH_HISP = toHH_HISP vals.[36]
        DRVRCNT = toFloat vals.[37]
        CDIVMSAR = toCDIVMSAR vals.[38]
        HHFAMINC = toHHFAMINC vals.[39]
        HHFAMINC_IMP = toHHFAMINC_IMP vals.[40]
        HH_RACE = toHH_RACE vals.[41]
        HHSIZE = toFloat vals.[42]
        HHVEHCNT = toFloat vals.[43]
        LIF_CYC = toLIF_CYC vals.[44]
        MSACAT = toMSACAT vals.[45]
        MSASIZE = toMSASIZE vals.[46]
        TRAVDAY = toTRAVDAY vals.[47]
        URBAN = toURBAN vals.[48]
        URBANSIZE = toURBANSIZE vals.[49]
        URBRUR = toURBRUR vals.[50]
        TDAYDATE = toDateTime vals.[51]
        WRKCOUNT = toFloat vals.[52]
        STRATUMID = string vals.[53]
        WTPERFIN = toFloat vals.[54]
        WTPERFIN5D = toFloat vals.[55]
        WTPERFIN2D = toFloat vals.[56]
        R_AGE = toFloat vals.[57]
        R_SEX = toBaseResponse vals.[58]
        WORKER = toBaseResponse vals.[59]
        DRIVER = toBaseResponse vals.[60]
        R_RACE = toBaseResponse vals.[61]
        R_HISP = toR_HISP vals.[62]
        PROXY = toPROXY vals.[63]
        EDUC = toBaseResponse vals.[64]
        R_SEX_IMP = toR_SEX_IMP vals.[65]
}