run: environments
	docker-compose -f docker-compose.yml stop 
	docker-compose -f docker-compose.yml build
	docker-compose -f docker-compose.yml up

configure:
	aws configure

environments:
ifndef AWS_ACCESS_KEY_ID
	$(error AWS_ACCESS_KEY_ID is undefined)
endif

ifndef AWS_SECRET_ACCESS_KEY
	$(error AWS_SECRET_ACCESS_KEY is undefined)
endif

ifndef AWS_REGION
	$(error AWS_REGION is undefined)
endif