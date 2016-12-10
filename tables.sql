CREATE TABLE `cartelle` (
 `versione` int(11) NOT NULL,
 `username` varchar(50) NOT NULL,
 `data` datetime NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=latin1


CREATE TABLE `files` (
 `username` varchar(50) NOT NULL,
 `filename` varchar(255) NOT NULL,
 `hash` char(32) NOT NULL,
 `path` int(11) NOT NULL,
 `folder_version` int(11) NOT NULL,
 PRIMARY KEY (`username`,`filename`,`hash`,`folder_version`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1

	
CREATE TABLE `utenti` (
 `username` varchar(50) NOT NULL,
 `password` char(40) NOT NULL,
 `folder` varchar(220) DEFAULT NULL,
 PRIMARY KEY (`username`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1