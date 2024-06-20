namespace FsOpenAI.TravelSurvey.Types
open System

type Response = 
    | R_NotAscertained
    | R_Skipped
    | Value of float

/// Household income
type HHFAMINC =
| ``HHFAMINC_I prefer not to answer``
| ``HHFAMINC_I don't know``
| ``HHFAMINC_Less than 10000``
| ``HHFAMINC_10000 to 14999``
| ``HHFAMINC_15000 to 24999``
| ``HHFAMINC_25000 to 34999``
| ``HHFAMINC_35000 to 49999``
| ``HHFAMINC_50000 to 74999``
| ``HHFAMINC_75000 to 99999``
| ``HHFAMINC_100000 to 124999``
| ``HHFAMINC_125000 to 149999``
| ``HHFAMINC_150000 to 199999``
| ``HHFAMINC_200000 or more``

/// Respondent sex
type R_SEX =
| R_SEX_Refuse
| ``R_SEX_Don't know``
| R_SEX_Male
| R_SEX_Female

/// Vehicle make ID
type MAKE =
| ``MAKE_Suppressed for confidential reason``
| MAKE_Jeep
| MAKE_Chrysler
| MAKE_Dodge
| MAKE_Ford
| MAKE_Lincoln
| ``MAKE_Buick  Opel``
| MAKE_Cadillac
| MAKE_Chevrolet
| MAKE_Pontiac
| MAKE_GMC
| ``MAKE_Other Domestic Manufacturers (eg Tesla)``
| MAKE_Volkswagen
| MAKE_Audi
| MAKE_BMW
| ``MAKE_NissanDatsun``
| MAKE_Honda
| MAKE_Mazda
| ``MAKE_Mercedes-Benz``
| MAKE_Subaru
| MAKE_Toyota
| MAKE_Volvo
| MAKE_Mitsubishi
| MAKE_Acura
| MAKE_Hyundai
| MAKE_Infiniti
| MAKE_Lexus
| MAKE_KIA
| ``MAKE_Harley-Davidson``
| ``MAKE_Other Make``

/// Urban area size where home address is located
type URBANSIZE =
| ``URBANSIZE_50000-199999``
| ``URBANSIZE_200000-499999``
| ``URBANSIZE_500000-999999``
| ``URBANSIZE_1000000 or more with heavy rail``
| ``URBANSIZE_1000000 or more without heavy rail``
| ``URBANSIZE_Not in urbanized area``

/// All HH members completed survey?
type FLAG100 =
| ``FLAG100_All eligible household members completed``
| ``FLAG100_75% to 99% of eligible household members completed``

/// Flag indicating at least 2 persons in HH are related
type HHRELATD =
| ``HHRELATD_At least two persons in hh are related``
| ``HHRELATD_No related persons in hh``

/// Vehicle type
type VEHTYPE =
| ``VEHTYPE_AutomobileCarStationwagon``
| ``VEHTYPE_Van (MinivanCargoPassenger)``
| ``VEHTYPE_SUV (Santa Fe Tahoe Jeep etc)``
| ``VEHTYPE_Pickup Truck``
| ``VEHTYPE_Other Truck``
| ``VEHTYPE_Recreational vehicle (RV)Motorhome``
| VEHTYPE_MotorcycleMoped
| ``VEHTYPE_Something else``

/// Trip mode, derived
type TRPTRANS =
| TRPTRANS_Car
| TRPTRANS_Van
| ``TRPTRANS_SUVCrossover``
| ``TRPTRANS_Pickup truck``
| ``TRPTRANS_Recreational Vehicle``
| TRPTRANS_Motorcycle
| ``TRPTRANS_Public or commuter bus``
| ``TRPTRANS_School bus``
| ``TRPTRANS_Street car or trolley car``
| ``TRPTRANS_Subway or elevated rail``
| ``TRPTRANS_Commuter rail``
| TRPTRANS_Amtrak
| TRPTRANS_Airplane
| ``TRPTRANS_Taxicab or limo service``
| ``TRPTRANS_Other ride-sharing service``
| ``TRPTRANS_Paratransit Dial a ride``
| ``TRPTRANS_Bicycle (including bikeshare ebike etc)``
| ``TRPTRANS_E-scooter``
| TRPTRANS_Walked
| ``TRPTRANS_Other (specify)``

/// Person 5 or older - Hispanic or Latino
type R_HISP =
| R_HISP_Hispanic
| ``R_HISP_Not Hispanic``

/// Hispanic status of household respondent
type HH_HISP =
| ``HH_HISP_Hispanic or Latino``
| ``HH_HISP_Not Hispanic or Latino``

/// Trip purpose summary
type WHYTRP1S =
| WHYTRP1S_Home
| WHYTRP1S_Work
| ``WHYTRP1S_SchoolDaycareReligious``
| ``WHYTRP1S_MedicalDental services``
| ``WHYTRP1S_ShoppingErrands``
| ``WHYTRP1S_SocialRecreational``
| ``WHYTRP1S_Transport someone``
| WHYTRP1S_Meals
| ``WHYTRP1S_Something else``

/// Population size category of the MSA from the five-year ACS API
type MSASIZE =
| ``MSASIZE_In an MSA of Less than 250000``
| ``MSASIZE_In an MSA of 250000 - 499999``
| ``MSASIZE_In an MSA of 500000 - 999999``
| ``MSASIZE_In an MSA or CMSA of 1000000 - 2999999``
| ``MSASIZE_In an MSA or CMSA of 3 million or more``
| ``MSASIZE_Not in MSA or CMSA``

/// Household urban area classification, based on 2020 TIGER/Line Shapefile
type URBAN =
| ``URBAN_In an urban area``
| ``URBAN_In an Urban cluster``
| ``URBAN_In an area surrounded by urban areas``
| ``URBAN_Not in urban area``

/// Household income (imputed)
type HHFAMINC_IMP =
| ``HHFAMINC_IMP_Less than 10000``
| ``HHFAMINC_IMP_10000 to 14999``
| ``HHFAMINC_IMP_15000 to 24999``
| ``HHFAMINC_IMP_25000 to 34999``
| ``HHFAMINC_IMP_35000 to 49999``
| ``HHFAMINC_IMP_50000 to 74999``
| ``HHFAMINC_IMP_75000 to 99999``
| ``HHFAMINC_IMP_100000 to 124999``
| ``HHFAMINC_IMP_125000 to 149999``
| ``HHFAMINC_IMP_150000 to 199999``
| ``HHFAMINC_IMP_200000 or more``

/// Trip origin and destination at Identical location
type LOOP_TRIP =
| ``LOOP_TRIP_Loop trip``
| ``LOOP_TRIP_Not a loop trip``

/// Respondent sex (imputed)
type R_SEX_IMP =
| R_SEX_IMP_Male
| R_SEX_IMP_Female

/// MSA category for the HH home address
type MSACAT =
| ``MSACAT_MSA of 1 million or more with rail``
| ``MSACAT_MSA of 1 million or more without rail``
| ``MSACAT_MSA less than 1 million``
| ``MSACAT_Not in MSA``

/// Census division classification for home address
type CENSUS_D =
| ``CENSUS_D_New England``
| ``CENSUS_D_Middle Atlantic``
| ``CENSUS_D_East North Central``
| ``CENSUS_D_West North Central``
| ``CENSUS_D_South Atlantic``
| ``CENSUS_D_East South Central``
| ``CENSUS_D_West South Central``
| CENSUS_D_Mountain
| CENSUS_D_Pacific

/// Census region classification for home address
type CENSUS_R =
| CENSUS_R_Northeast
| CENSUS_R_Midwest
| CENSUS_R_South
| CENSUS_R_West

/// Life Cycle classification for the household
type LIF_CYC =
| ``LIF_CYC_one adult no children``
| ``LIF_CYC_2  adults no children``
| ``LIF_CYC_one adult youngest child 0-5``
| ``LIF_CYC_2  adults youngest child 0-5``
| ``LIF_CYC_one adult youngest child 6-15``
| ``LIF_CYC_2  adults youngest child 6-15``
| ``LIF_CYC_one adult youngest child 16-21``
| ``LIF_CYC_2  adults youngest child 16-21``
| ``LIF_CYC_one adult retired no children``
| ``LIF_CYC_2  adults retired no children``

/// Type of home
type HOMETYPE =
| ``HOMETYPE_One-family detached``
| ``HOMETYPE_One-family attached (townhome condo)``
| ``HOMETYPE_Building with 2 or more apartments``
| ``HOMETYPE_Mobile home``
| ``HOMETYPE_Boat RV van etc``

/// Whether home owned or rented
type HOMEOWN =
| ``HOMEOWN_Owned by hh member with mortgage or loan``
| ``HOMEOWN_Owned by hh member free and clear (no mortgage)``
| ``HOMEOWN_Rented by hh member``
| ``HOMEOWN_Occupied without payment``

/// Reason for travel to destination
type WHYTO =
| ``WHYTO_Regular activities at home``
| ``WHYTO_Work from home (paid)``
| ``WHYTO_Work at a non-home location``
| ``WHYTO_Work activity to drop-offpickup someonesomething``
| ``WHYTO_Other work-related activities``
| ``WHYTO_Attend school as a student``
| ``WHYTO_Attend childcare or adult care``
| ``WHYTO_Volunteer activities (not paid)``
| ``WHYTO_Change type of transportation``
| ``WHYTO_Drop offpick up someone (personal)``
| ``WHYTO_Health care visit``
| ``WHYTO_Buy meals``
| ``WHYTO_Shopbuypick-up or return goods``
| ``WHYTO_Other familypersonal errands``
| ``WHYTO_Recreational activities``
| WHYTO_Exercise
| ``WHYTO_Visit friends or relatives``
| ``WHYTO_Rest or relaxationvacation``
| ``WHYTO_Religious or other community activities``
| ``WHYTO_Something else (specify)``

/// Person 1  was on trip
type ONTD_P1 =
| ONTD_P1_Selected
| ``ONTD_P1_Not Selected``

/// Survey completed by self or someone else
type PROXY =
| PROXY_Self
| ``PROXY_Someone else``

/// Travel day - day of week
type TRAVDAY =
| TRAVDAY_Sunday
| TRAVDAY_Monday
| TRAVDAY_Tuesday
| TRAVDAY_Wednesday
| TRAVDAY_Thursday
| TRAVDAY_Friday
| TRAVDAY_Saturday

/// Travel day trip purpose consistent with 1990 NPTS design
type WHYTRP90 =
| ``WHYTRP90_ToFrom Work``
| ``WHYTRP90_Work-Related Business``
| WHYTRP90_Shopping
| ``WHYTRP90_Other FamilyPersonal Business``
| ``WHYTRP90_SchoolChurch``
| ``WHYTRP90_MedicalDental``
| ``WHYTRP90_Visit FriendsRelatives``
| ``WHYTRP90_Other SocialRecreational``
| WHYTRP90_Other
| ``WHYTRP90_Refused  Don't Know``

/// Household in urban/rural area
type URBRUR =
| URBRUR_Urban
| URBRUR_Rural

/// Used public transit on trip
type PUBTRANS =
| ``PUBTRANS_Used public transit``
| ``PUBTRANS_Did not use public transit``

/// Weekend trip
type TDWKND =
| TDWKND_Weekend
| TDWKND_Weekday

/// Race of household respondent
type HH_RACE =
| HH_RACE_White
| ``HH_RACE_Black or African American``
| HH_RACE_Asian
| ``HH_RACE_American IndianAlaska Native``
| ``HH_RACE_Native HawaiianPacific Islander``
| ``HH_RACE_Multiple races selected``
| ``HH_RACE_Other race``

type YesNo = 
    | YN_NotAscertained
    | YN_Skipped
    | Yes
    | No

/// Grouping of household by combination of Census division, MSA status, and presence of rail
type CDIVMSAR =
| ``CDIVMSAR_New England MSACMSA of 1 million  with heavy rail``
| ``CDIVMSAR_New England MSACMSA of 1 million  wo heavy rail``
| ``CDIVMSAR_New England MSA of less than 1 million``
| ``CDIVMSAR_New England Not in an MSA``
| ``CDIVMSAR_Mid-Atlantic MSACMSA of 1 million  with heavy rail``
| ``CDIVMSAR_Mid-Atlantic MSACMSA of 1 million  wo heavy rail``
| ``CDIVMSAR_Mid-Atlantic MSA of less than 1 million``
| ``CDIVMSAR_Mid-Atlantic Not in an MSA``
| ``CDIVMSAR_East North Central MSACMSA of 1 million  with heavy rail``
| ``CDIVMSAR_East North Central MSACMSA of 1 million  wo heavy rail``
| ``CDIVMSAR_East North Central MSA of less than 1 million``
| ``CDIVMSAR_East North Central Not in an MSA``
| ``CDIVMSAR_West North Central MSACMSA of 1 million  wo heavy rail``
| ``CDIVMSAR_West North Central MSA of less than 1 million``
| ``CDIVMSAR_West North Central Not in an MSA``
| ``CDIVMSAR_South Atlantic MSACMSA of 1 million  with heavy rail``
| ``CDIVMSAR_South Atlantic MSACMSA of 1 million  wo heavy rail``
| ``CDIVMSAR_South Atlantic MSA of less than 1 million``
| ``CDIVMSAR_South Atlantic Not in an MSA``
| ``CDIVMSAR_East South Central MSACMSA of 1 million  wo heavy rail``
| ``CDIVMSAR_East South Central MSA of less than 1 million``
| ``CDIVMSAR_East South Central Not in an MSA``
| ``CDIVMSAR_West South Central MSACMSA of 1 million  wo heavy rail``
| ``CDIVMSAR_West South Central MSA of less than 1 million``
| ``CDIVMSAR_West South Central Not in an MSA``
| ``CDIVMSAR_Mountain MSACMSA of 1 million  wo heavy rail``
| ``CDIVMSAR_Mountain MSA of less than 1 million``
| ``CDIVMSAR_Mountain Not in an MSA``
| ``CDIVMSAR_Pacific MSACMSA of 1 million  with heavy rail``
| ``CDIVMSAR_Pacific MSACMSA of 1 million  wo heavy rail``
| ``CDIVMSAR_Pacific MSA of less than 1 million``
| ``CDIVMSAR_Pacific Not in an MSA``

type Household = {
    HOUSEID : string // Unique Identifier- Household
    WTHHFIN : float // 7-day natl household weight
    WTHHFIN5D : float // 5-day natl household weight
    WTHHFIN2D : float // 2-day natl household weight
    NUMADLT : float // Count of adult household members at least 18 years old
    HOMEOWN : HOMEOWN // Whether home owned or rented
    HOMETYPE : HOMETYPE // Type of home
    RAIL : YesNo // MSA heavy rail status for household
    CENSUS_D : CENSUS_D // Census division classification for home address
    CENSUS_R : CENSUS_R // Census region classification for home address
    HH_HISP : HH_HISP // Hispanic status of household respondent
    DRVRCNT : float // Number of drivers in the household
    CNTTDHH : float // Count of household trips on travel day
    CDIVMSAR : CDIVMSAR // Grouping of household by combination of Census division, MSA status, and presence of rail
    FLAG100 : FLAG100 // All HH members completed survey?
    HHFAMINC : HHFAMINC // Household income
    HHFAMINC_IMP : HHFAMINC_IMP // Household income (imputed)
    HH_RACE : HH_RACE // Race of household respondent
    HHSIZE : float // Total number of people in household
    HHVEHCNT : float // Total number of vehicles in household
    HHRELATD : HHRELATD // Flag indicating at least 2 persons in HH are related
    LIF_CYC : LIF_CYC // Life Cycle classification for the household
    MSACAT : MSACAT // MSA category for the HH home address
    MSASIZE : MSASIZE // Population size category of the MSA from the five-year ACS API
    TRAVDAY : TRAVDAY // Travel day - day of week
    URBAN : URBAN // Household urban area classification, based on 2020 TIGER/Line Shapefile
    URBANSIZE : URBANSIZE // Urban area size where home address is located
    URBRUR : URBRUR // Household in urban/rural area
    PPT517 : float // Count of household members 5-17 years old
    YOUNGCHILD : float // Count of household members under 5 years old
    RESP_CNT : float // Count of responding persons in household
    URBRUR_2010 : URBRUR // Household in urban/rural area based on 2010 Census
    TDAYDATE : DateTime // Date of travel day (YYYYMM)
    WRKCOUNT : float // Count of workers in household
    STRATUMID : string // Household Stratum ID
}

type Vehicle = {
    HOUSEID : string // Unique Identifier- Household
    VEHID : string // Vehicle ID within household
    VEHYEAR : float // Vehicle year
    MAKE : MAKE // Vehicle make ID
    HHVEHCNT : float // Total number of vehicles in household
    VEHTYPE : Response // Vehicle type
    VEHFUEL : Response // Type of fuel vehicle runs on
    VEHCOMMERCIAL : Response // Vehicle used for business purposes
    VEHCOM_RS : Response // Vehicle used for rideshare
    VEHCOM_DEL : Response // Vehicle used for delivery service
    VEHCOM_OTH : Response // Vehicle used for other business purposes
    COMMERCIALFREQ : Response // Over past 30 days, how many days was vehicle used for business purposes?
    HHVEHUSETIME_RS : Response // Over past 30 days, how many days was vehicle used for rideshare?
    HHVEHUSETIME_DEL : Response // Over past 30 days, how many days was vehicle used for deliveries?
    HHVEHUSETIME_OTH : Response // Over past 30 days, how many days was vehicle used for other business?
    VEHOWNED : Response // Vehicle owned for 1 year or more
    WHOMAIN : Response // Main driver of vehicle
    VEHCASEID : Response // Unique vehicle identifier
    ANNMILES : Response // Self-reported annualized mile estimate
    HYBRID : YesNo // Hybrid vehicle
    VEHAGE : float // Age of vehicle, based on model year
    VEHOWNMO : Response // Vehicles owned less than 1 year - months owned
    NUMADLT : float // Count of adult household members at least 18 years old
    HOMEOWN : HOMEOWN // Whether home owned or rented
    RAIL : YesNo // MSA heavy rail status for household
    CENSUS_D : CENSUS_D // Census division classification for home address
    CENSUS_R : CENSUS_R // Census region classification for home address
    HH_HISP : HH_HISP // Hispanic status of household respondent
    DRVRCNT : float // Number of drivers in the household
    CDIVMSAR : CDIVMSAR // Grouping of household by combination of Census division, MSA status, and presence of rail
    HHFAMINC : HHFAMINC // Household income
    HH_RACE : HH_RACE // Race of household respondent
    HHSIZE : float // Total number of people in household
    LIF_CYC : LIF_CYC // Life Cycle classification for the household
    MSACAT : MSACAT // MSA category for the HH home address
    MSASIZE : MSASIZE // Population size category of the MSA from the five-year ACS API
    TRAVDAY : TRAVDAY // Travel day - day of week
    URBAN : URBAN // Household urban area classification, based on 2020 TIGER/Line Shapefile
    URBANSIZE : URBANSIZE // Urban area size where home address is located
    URBRUR : URBRUR // Household in urban/rural area
    TDAYDATE : DateTime // Date of travel day (YYYYMM)
    WRKCOUNT : float // Count of workers in household
    STRATUMID : string // Household Stratum ID
    WTHHFIN : float // 7-day natl household weight
    WTHHFIN5D : float // 5-day natl household weight
    WTHHFIN2D : float // 2-day natl household weight
    HHFAMINC_IMP : HHFAMINC_IMP // Household income (imputed)
}

type Person = {
    HOUSEID : string // Unique Identifier- Household
    PERSONID : string // Person ID within household
    WTPERFIN : float // 7 day National person weight
    WTPERFIN5D : float // 5 day National person weight
    WTPERFIN2D : float // 2 day National person weight
    R_AGE : float // Respondent age
    R_SEX : Response // Respondent sex
    R_RELAT : Response // Respondent relationship to primary respondent
    WORKER : Response // Employment status of respondent
    DRIVER : Response // Driver status, derived
    R_RACE : Response // Respondent race
    GCDWORK : Response // Great circle distance (miles) between home and work
    OUTOFTWN : YesNo // Away from home entire travel day
    USEPUBTR : YesNo // Public Transit Usage on Travel Date
    R_RACE_IMP : HH_RACE // Respondent race (imputed)
    R_HISP : R_HISP // Person 5 or older - Hispanic or Latino
    PROXY : PROXY // Survey completed by self or someone else
    WHOPROXY : Response // Who is completing the survey
    EDUC : Response // Highest level of education
    LAST30_TAXI : Response // Used taxi service in last 30 days
    LAST30_RDSHR : Response // Used rideshare in last 30 days
    LAST30_ESCT : Response // Used e-scooters in last 30 days
    LAST30_PT : Response // Used public transit in last 30 days
    LAST30_MTRC : Response // Used motorcycle in last 30 days
    LAST30_WALK : Response // Walked from place to place in last 30 days
    LAST30_BIKE : Response // Used bicycle in last 30 days
    LAST30_BKSHR : Response // Used bike share in last 30 days
    TAXISERVICE : Response // Days in last 30 days taxi service used
    RIDESHARE22 : Response // Days in last 30 days rideshare used
    ESCOOTERUSED : Response // Days in last 30 days e-scooter used
    PTUSED : Response // Days in last 30 days public transit used
    TRNPASS : Response // Discounted transit pass used in past 30 days
    MCTRANSIT : Response // Days in last 30 days motorcycle used
    WALKTRANSIT : Response // Days in last 30 days walking used
    BIKETRANSIT : Response // Days in last 30 days cycling used
    BIKESHARE22 : Response // Days in last 30 days bike share used
    USAGE1 : Response // Fewer trips in past 30 days
    USAGE2_1 : Response // Reason fewer trips - more deliveries
    USAGE2_2 : Response // Reason fewer trips - did not feel safe
    USAGE2_3 : Response // Reason fewer trips - did not feel clean/healthy
    USAGE2_4 : Response // Reason fewer trips - not reliable
    USAGE2_5 : Response // Reason fewer trips - did not go where needed
    USAGE2_6 : Response // Reason fewer trips - unaffordable
    USAGE2_7 : Response // Reason fewer trips - health problems
    USAGE2_8 : Response // Reason fewer trips - no time
    USAGE2_9 : Response // Reason fewer trips - other
    USAGE2_10 : Response // Reason fewer trips - COVID 19
    QACSLAN1 : Response // Language other than English spoken at home
    QACSLAN3 : Response // How well this person speaks English
    PAYPROF : Response // Worked for pay last week
    PRMACT : Response // Primary activity for those who did not work for pay last week
    EMPLOYMENT2 : Response // Hours worked for pay each week
    DRIVINGOCCUPATION : Response // Drive for work
    DRIVINGVEHICLE : Response // Vehicle driven for work
    WRKLOC : Response // Description of work location
    WKFMHM22 : Response // Days per week worked from home
    WRKTRANS : Response // Usual transport to work
    EMPPASS : Response // Employer pays for discounted transit pass
    SCHOOL1 : Response // Enrolled in school or academic program
    STUDE : Response // School or academic program description
    SCHTYP : Response // Type of K-12 school enrolled in
    SCHOOL1C : Response // Type of non K-12 school enrolled in
    SCHTRN1 : Response // Usual transport to school
    DELIVER : Response // Number of online purchase deliveries in past 30 days
    DELIV_GOOD : Response // Number of times goods delivered in past 30 days
    DELIV_FOOD : Response // Number of times food delivered in past 30 days
    DELIV_GROC : Response // Number of times groceries delivered in past 30 days
    DELIV_PERS : Response // Number of times services delivered in the past 30 days
    RET_HOME : Response // Number of times returned online purchase by home pickup
    RET_PUF : Response // Number of times returned online purchase to post office/UPS/Fed Ex/ similar
    RET_AMZ : Response // Number of times returned online purchase at Amazon dropoff center
    RET_STORE : Response // Number of times returned online purchase by direct to store
    MEDCOND : Response // Condition or disability that makes travel difficult
    MEDCOND6 : Response // Length of time respondent has had condition
    W_CANE : Response // Uses cane or walking stick
    W_WKCR : Response // Uses walker or crutches
    W_VISIMP : Response // Uses devices to aid the blind or visually impaired
    W_SCCH : Response // Uses motorized scooter or wheelchair
    W_CHAIR : Response // Uses manual scooter or wheel chair
    W_NONE : Response // Uses no medical device for mobility
    CONDTRAV : Response // Reduced travel due to condition or disability
    CONDRIDE : Response // Asked others for rides due to condition or disability
    CONDNIGH : Response // Limited driving to daytime due to condition or disability
    CONDRIVE : Response // Given up driving due to condition or disability
    CONDPUB : Response // Used bus or subway less frequently due to condition or disability
    CONDSPEC : Response // Used special transportation services due to condition or disability
    CONDSHARE : Response // Used rideshare due to condition or disability
    CONDNONE : Response // Travel not affected by condition or disability
    CONDRF : Response // Prefer not to answer if travel is affected by condition or disability
    FRSTHM : YesNo // Started travel day at home
    PARK : Response // Paid for parking at any time during travel day
    PARKHOME : Response // Pay for home parking
    PARKHOMEAMT : Response // Whether respondent pays to park at home
    PARKHOMEAMT_PAMOUNT : Response // Cost of parking at home
    PARKHOMEAMT_PAYTYPE : Response // Duration of payment
    SAMEPLC : Response // Reason for not taking trips on travel day
    COV1_WK : Response // COVID impact on travel to a physical work location
    COV1_SCH : Response // COVID impact on travel to a physical school/class location
    COV1_PT : Response // COVID impact on use of public transit
    COV1_OHD : Response // COVID impact on online purchases for home delivery
    COV2_WK : Response // Work travel changes temporary or permanent
    COV2_SCH : Response // School travel changes temporary or permanent
    COV2_PT : Response // Public transit use changes temporary or permanent
    COV2_OHD : Response // Home delivery changes temporary or permanent
    CNTTDTR : float // Count of person trips on travel day
    R_SEX_IMP : R_SEX_IMP // Respondent sex (imputed)
    NUMADLT : float // Count of adult household members at least 18 years old
    HOMEOWN : HOMEOWN // Whether home owned or rented
    RAIL : YesNo // MSA heavy rail status for household
    CENSUS_D : CENSUS_D // Census division classification for home address
    CENSUS_R : CENSUS_R // Census region classification for home address
    HH_HISP : HH_HISP // Hispanic status of household respondent
    DRVRCNT : float // Number of drivers in the household
    CDIVMSAR : CDIVMSAR // Grouping of household by combination of Census division, MSA status, and presence of rail
    HHFAMINC : HHFAMINC // Household income
    HH_RACE : HH_RACE // Race of household respondent
    HHSIZE : float // Total number of people in household
    HHVEHCNT : float // Total number of vehicles in household
    LIF_CYC : LIF_CYC // Life Cycle classification for the household
    MSACAT : MSACAT // MSA category for the HH home address
    MSASIZE : MSASIZE // Population size category of the MSA from the five-year ACS API
    TRAVDAY : TRAVDAY // Travel day - day of week
    URBAN : URBAN // Household urban area classification, based on 2020 TIGER/Line Shapefile
    URBANSIZE : URBANSIZE // Urban area size where home address is located
    URBRUR : URBRUR // Household in urban/rural area
    TDAYDATE : DateTime // Date of travel day (YYYYMM)
    WRKCOUNT : float // Count of workers in household
    STRATUMID : string // Household Stratum ID
    HHFAMINC_IMP : HHFAMINC_IMP // Household income (imputed)
}

type Trip = {
    HOUSEID : string // Unique Identifier- Household
    PERSONID : string // Person ID within household
    TRIPID : string // Trip ID for each trip a person took
    SEQ_TRIPID : string // Renumbered sequential tripid
    VEHCASEID : Response // Unique vehicle identifier
    FRSTHM : YesNo // Started travel day at home
    PARK : Response // Paid for parking at any time during travel day
    HHMEMDRV : Response // Household member drove on trip
    TDWKND : TDWKND // Weekend trip
    TRAVDAY : TRAVDAY // Travel day - day of week
    LOOP_TRIP : LOOP_TRIP // Trip origin and destination at Identical location
    DWELTIME : Response // Time at Destination (minutes)
    PUBTRANS : PUBTRANS // Used public transit on trip
    TRIPPURP : Response // General purpose of trip
    WHYFROM : Response // Reason for previous trip
    WHYTRP1S : WHYTRP1S // Trip purpose summary
    TRVLCMIN : Response // Trip Duration in Minutes
    STRTTIME : Response // 24 hour local start time of trip
    ENDTIME : Response // 24 hour local end time of trip
    TRPHHVEH : Response // Household vehicle used for trip
    VEHID : string // Vehicle ID within household
    TRPTRANS : TRPTRANS // Trip mode, derived
    NUMONTRP : float // Number of people on trip
    ONTD_P1 : ONTD_P1 // Person 1 was on trip
    ONTD_P2 : Response // Person 2 was on trip
    ONTD_P3 : Response // Person 3 was on trip
    ONTD_P4 : Response // Person 4 was on trip
    ONTD_P5 : Response // Person 5 was on trip
    ONTD_P6 : Response // Person 6 was on trip
    ONTD_P7 : Response // Person 7 was on trip
    ONTD_P8 : Response // Person 8 was on trip
    ONTD_P9 : Response // Person 9 was on trip
    ONTD_P10 : Response // Person 10 was on trip
    NONHHCNT : float // Number of non-household members on trip
    HHACCCNT : float // Number of household members on trip
    WHODROVE : Response // Person who drove on trip
    DRVR_FLG : Response // Flag for driver on trip
    PSGR_FLG : Response // Flag for passenger on trip
    WHODROVE_IMP : Response // Imputed person who drove on trip
    PARK2_PAMOUNT : Response // Amount paid for parking
    PARK2_PAYTYPE : Response // Periodicity of parking payment
    PARK2 : Response // Paid for parking on this trip
    WHYTO : WHYTO // Reason for travel to destination
    WALK : Response // Minutes walked from parking to destination
    TRPMILES : Response // Calculated Trip distance converted into miles
    WTTRDFIN : float // 7 day National trip weight
    WTTRDFIN5D : float // 5 day National trip weight
    WTTRDFIN2D : float // 2 day National trip weight
    TDCASEID : string // Unique identifier for every trip record in the file
    VMT_MILE : Response // Calculated Trip distance (miles) for Driver Trips
    GASPRICE : float // Weekly regional gasoline price, in cents, during the week of the household's travel day
    WHYTRP90 : WHYTRP90 // Travel day trip purpose consistent with 1990 NPTS design
    NUMADLT : float // Count of adult household members at least 18 years old
    HOMEOWN : HOMEOWN // Whether home owned or rented
    RAIL : YesNo // MSA heavy rail status for household
    CENSUS_D : CENSUS_D // Census division classification for home address
    CENSUS_R : CENSUS_R // Census region classification for home address
    HH_HISP : HH_HISP // Hispanic status of household respondent
    DRVRCNT : float // Number of drivers in the household
    CDIVMSAR : CDIVMSAR // Grouping of household by combination of Census division, MSA status, and presence of rail
    HHFAMINC : HHFAMINC // Household income
    HH_RACE : HH_RACE // Race of household respondent
    HHSIZE : float // Total number of people in household
    HHVEHCNT : float // Total number of vehicles in household
    LIF_CYC : LIF_CYC // Life Cycle classification for the household
    MSACAT : MSACAT // MSA category for the HH home address
    MSASIZE : MSASIZE // Population size category of the MSA from the five-year ACS API
    URBAN : URBAN // Household urban area classification, based on 2020 TIGER/Line Shapefile
    URBANSIZE : URBANSIZE // Urban area size where home address is located
    URBRUR : URBRUR // Household in urban/rural area
    TDAYDATE : DateTime // Date of travel day (YYYYMM)
    WRKCOUNT : float // Count of workers in household
    STRATUMID : string // Household Stratum ID
    R_AGE : float // Respondent age
    R_SEX : Response // Respondent sex
    WORKER : Response // Employment status of respondent
    DRIVER : Response // Driver status, derived
    R_RACE : Response // Respondent race
    R_HISP : R_HISP // Person 5 or older - Hispanic or Latino
    PROXY : PROXY // Survey completed by self or someone else
    EDUC : Response // Highest level of education
    PRMACT : Response // Primary activity for those who did not work for pay last week
    R_SEX_IMP : R_SEX_IMP // Respondent sex (imputed)
    VEHTYPE : Response // Vehicle type
    HHFAMINC_IMP : HHFAMINC_IMP // Household income (imputed)
}

type LongTrip = {
    HOUSEID : string // Unique Identifier- Household
    PERSONID : string // Person ID within household
    LONGDIST : float // Number of long distance trips in past 30 days
    MAINMODE : Response // Mode of travel for last long distance trip
    INT_FLAG : Response // Farthest destination on the trip was in the US or outside US
    LD_NUMONTRP : Response // Number of people with respondent on long distance trip
    ONTP_P1 : Response // Person 1 on long distance trip
    ONTP_P2 : Response // Person 2 on long distance trip
    ONTP_P3 : Response // Person 3 on long distance trip
    ONTP_P4 : Response // Person 4 on long distance trip
    ONTP_P5 : Response // Person 5 on long distance trip
    ONTP_P6 : Response // Person 6 on long distance trip
    ONTP_P7 : Response // Person 7 on long distance trip
    ONTP_P8 : Response // Person 8 on long distance trip
    ONTP_P9 : Response // Person 9 on long distance trip
    ONTP_P10 : Response // Person 10 on long distance trip
    FARREAS : Response // Main reason for most recent long distance trip
    LD_AMT : float // Number of times used AMTRAK in past year
    LD_ICB : float // Number of times used Intra-City bus in past year
    LDT_FLAG : Response // Source of long distance data
    BEGTRIP : Response // Beginning date of trip (YYYYMM)
    ENDTRIP : Response // Ending date of trip (YYYYMM)
    NTSAWAY : Response // Nights away on long distance trip
    WEEKEND : Response // Trip includes weekend
    MRT_DATE : Response // Date of most recent long distance trip (YYYYMM)
    FARCDIV : Response // Farthest domestic destination Census division code (domestic trips only)
    FARCREG : Response // Farthest domestic destination Census region FIPS code (domestic trips only)
    GCDTOT : Response // Great circle distance from home to farthest domestic dest (domestic trips only)
    AIRSIZE : Response // Domestic airport hub size based on FY23 NPIAS hub type status (international trips only)
    EXITCDIV : Response // Census division at which respondent exited the US
    GCD_FLAG : Response // Flag for long distance trips of 50 miles or more
    NUMADLT : float // Count of adult household members at least 18 years old
    HOMEOWN : HOMEOWN // Whether home owned or rented
    RAIL : YesNo // MSA heavy rail status for household
    CENSUS_D : CENSUS_D // Census division classification for home address
    CENSUS_R : CENSUS_R // Census region classification for home address
    HH_HISP : HH_HISP // Hispanic status of household respondent
    DRVRCNT : float // Number of drivers in the household
    CDIVMSAR : CDIVMSAR // Grouping of household by combination of Census division, MSA status, and presence of rail
    HHFAMINC : HHFAMINC // Household income
    HHFAMINC_IMP : HHFAMINC_IMP // Household income (imputed)
    HH_RACE : HH_RACE // Race of household respondent
    HHSIZE : float // Total number of people in household
    HHVEHCNT : float // Total number of vehicles in household
    LIF_CYC : LIF_CYC // Life Cycle classification for the household
    MSACAT : MSACAT // MSA category for the HH home address
    MSASIZE : MSASIZE // Population size category of the MSA from the five-year ACS API
    TRAVDAY : TRAVDAY // Travel day - day of week
    URBAN : URBAN // Household urban area classification, based on 2020 TIGER/Line Shapefile
    URBANSIZE : URBANSIZE // Urban area size where home address is located
    URBRUR : URBRUR // Household in urban/rural area
    TDAYDATE : DateTime // Date of travel day (YYYYMM)
    WRKCOUNT : float // Count of workers in household
    STRATUMID : string // Household Stratum ID
    WTPERFIN : float // 7 day National person weight
    WTPERFIN5D : float // 5 day National person weight
    WTPERFIN2D : float // 2 day National person weight
    R_AGE : float // Respondent age
    R_SEX : Response // Respondent sex
    WORKER : Response // Employment status of respondent
    DRIVER : Response // Driver status, derived
    R_RACE : Response // Respondent race
    R_HISP : R_HISP // Person 5 or older - Hispanic or Latino
    PROXY : PROXY // Survey completed by self or someone else
    EDUC : Response // Highest level of education
    R_SEX_IMP : R_SEX_IMP // Respondent sex (imputed)
}


type DataSets = {
    Household   : Household list
    Vehicle     : Vehicle list
    Person      : Person list
    Trip        : Trip list
    LongTrip    : LongTrip list
}